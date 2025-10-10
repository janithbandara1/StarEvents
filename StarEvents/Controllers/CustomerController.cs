using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StarEvents.Models;
using Stripe.Checkout;
using QRCoder;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;

namespace StarEvents.Controllers
{
    public class CustomerController : BaseController
    {
        private readonly StarEventsDbContext _context;
        
        public CustomerController(StarEventsDbContext context)
        {
            _context = context;
        }
        
        public async Task<IActionResult> Dashboard()
        {
            var events = await _context.Events
                .Include(e => e.Organizer)
                .Where(e => e.Status == "Active" && e.EventDate >= DateTime.Now)
                .OrderBy(e => e.EventDate)
                .ToListAsync();
                
            // Get unique categories for filter dropdown
            var categories = await _context.Events
                .Where(e => e.Status == "Active" && !string.IsNullOrEmpty(e.Category))
                .Select(e => e.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
                
            ViewBag.Categories = categories;
            return View(events);
        }
        
        // API: Search and filter events
        [HttpGet]
        public async Task<IActionResult> SearchEvents(string search, string category, DateTime? date, string location)
        {
            var query = _context.Events
                .Include(e => e.Organizer)
                .Where(e => e.Status == "Active" && e.EventDate >= DateTime.Now);
            
            // Apply filters
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(e => e.Title.Contains(search) || 
                                       e.Description.Contains(search));
            }
            
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(e => e.Category == category);
            }
            
            if (date.HasValue)
            {
                var searchDate = date.Value.Date;
                query = query.Where(e => e.EventDate.Date == searchDate);
            }
            
            if (!string.IsNullOrEmpty(location))
            {
                query = query.Where(e => e.Location.Contains(location));
            }
            
            var events = await query
                .OrderBy(e => e.EventDate)
                .Select(e => new
                {
                    e.EventId,
                    e.Title,
                    e.Description,
                    e.Category,
                    e.Location,
                    EventDate = e.EventDate.ToString("MMM dd, yyyy"),
                    EventTime = e.EventDate.ToString("h:mm tt"),
                    e.TicketPrice,
                    OrganizerName = e.Organizer.UserName
                })
                .ToListAsync();
            
            return Json(new { success = true, data = events });
        }
        
        // Get event details for modal
        [HttpGet]
        public async Task<IActionResult> GetEventDetails(int id)
        {
            var eventDetails = await _context.Events
                .Include(e => e.Organizer)
                .Where(e => e.EventId == id && e.Status == "Active")
                .Select(e => new
                {
                    e.EventId,
                    e.Title,
                    e.Description,
                    e.Category,
                    e.Location,
                    EventDate = e.EventDate.ToString("MMM dd, yyyy"),
                    EventTime = e.EventDate.ToString("h:mm tt"),
                    e.TicketPrice,
                    OrganizerName = e.Organizer.UserName,
                    OrganizerEmail = e.Organizer.Email
                })
                .FirstOrDefaultAsync();
            
            if (eventDetails == null)
            {
                return Json(new { success = false, message = "Event not found." });
            }
            
            return Json(new { success = true, data = eventDetails });
        }
        
        // Create Stripe Checkout Session
        [HttpPost]
        public async Task<IActionResult> CreateCheckoutSession(int eventId, string discountCode = null, bool useLoyaltyPoints = false)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Json(new { success = false, message = "User not authenticated." });
            }
            
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }
            
            var eventItem = await _context.Events.FindAsync(eventId);
            if (eventItem == null || eventItem.Status != "Active")
            {
                return Json(new { success = false, message = "Event not found or inactive." });
            }
            
            decimal originalPrice = eventItem.TicketPrice;
            decimal discountAmount = 0;
            int loyaltyPointsUsed = 0;
            
            // Apply discount code if provided
            if (!string.IsNullOrWhiteSpace(discountCode))
            {
                var discount = await _context.Discounts
                    .FirstOrDefaultAsync(d => d.Code == discountCode && d.IsActive &&
                                             (!d.ValidFrom.HasValue || d.ValidFrom <= DateTime.Now) &&
                                             (!d.ValidTo.HasValue || d.ValidTo >= DateTime.Now));
                
                if (discount != null)
                {
                    discountAmount = originalPrice * discount.Percentage / 100;
                }
                else
                {
                    return Json(new { success = false, message = "Invalid or expired discount code." });
                }
            }
            
            // Apply loyalty points if requested
            if (useLoyaltyPoints)
            {
                var loyaltyPoint = await _context.LoyaltyPoints.FindAsync(userId);
                int availablePoints = loyaltyPoint?.Points ?? 0;
                
                // Assume 1 point = $0.01
                decimal maxLoyaltyDiscount = availablePoints * 0.01m;
                decimal loyaltyDiscount = Math.Min(maxLoyaltyDiscount, originalPrice - discountAmount);
                
                if (loyaltyDiscount > 0)
                {
                    loyaltyPointsUsed = (int)(loyaltyDiscount * 100); // Convert back to points
                    discountAmount += loyaltyDiscount;
                }
            }
            
            decimal finalPrice = Math.Max(0, originalPrice - discountAmount);
            
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(finalPrice * 100), // Amount in cents
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = eventItem.Title,
                                Description = eventItem.Description ?? "Event ticket"
                            }
                        },
                        Quantity = 1
                    }
                },
                Mode = "payment",
                SuccessUrl = Url.Action("PurchaseSuccess", "Customer", new { 
                    eventId = eventId, 
                    discountCode = discountCode, 
                    loyaltyPointsUsed = loyaltyPointsUsed 
                }, Request.Scheme),
                CancelUrl = Url.Action("Dashboard", "Customer", null, Request.Scheme),
                Metadata = new Dictionary<string, string>
                {
                    { "eventId", eventId.ToString() },
                    { "userId", user.UserId.ToString() },
                    { "discountCode", discountCode ?? "" },
                    { "loyaltyPointsUsed", loyaltyPointsUsed.ToString() }
                }
            };
            
            var service = new SessionService();
            Session session = service.Create(options);
            
            return Json(new { success = true, url = session.Url });
        }
        
        // Handle successful payment
        [HttpGet]
        public async Task<IActionResult> PurchaseSuccess(int eventId, string discountCode = null, int loyaltyPointsUsed = 0)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                TempData["Error"] = "User not authenticated.";
                return RedirectToAction("Dashboard");
            }
            
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("Dashboard");
            }
            
            var eventItem = await _context.Events.FindAsync(eventId);
            if (eventItem == null)
            {
                TempData["Error"] = "Event not found.";
                return RedirectToAction("Dashboard");
            }
            
            // Generate QR Code
            string qrCodeBase64 = GenerateQRCode(user.UserId, eventId);
            
            // Create ticket
            var ticket = new Ticket
            {
                EventId = eventId,
                UserId = user.UserId,
                PurchaseDate = DateTime.Now,
                PricePaid = eventItem.TicketPrice, // Store original price, or calculate actual paid?
                Status = "Purchased",
                QRCode = qrCodeBase64
            };
            
            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync();
            
            // Create payment record
            var payment = new Payment
            {
                TicketId = ticket.TicketId,
                Amount = eventItem.TicketPrice, // Actual amount paid after discount
                PaymentDate = DateTime.Now,
                PaymentMethod = "Stripe",
                Status = "Completed"
            };
            
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
            
            // Deduct loyalty points if used
            if (loyaltyPointsUsed > 0)
            {
                var loyaltyPoint = await _context.LoyaltyPoints.FindAsync(userId);
                if (loyaltyPoint != null)
                {
                    loyaltyPoint.Points -= loyaltyPointsUsed;
                    loyaltyPoint.LastUpdated = DateTime.Now;
                    _context.Update(loyaltyPoint);
                    await _context.SaveChangesAsync();
                }
            }
            
            // Award loyalty points (1 point per $1 spent)
            int pointsEarned = (int)eventItem.TicketPrice; // Or actual paid amount?
            var existingLoyaltyPoint = await _context.LoyaltyPoints.FindAsync(userId);
            if (existingLoyaltyPoint == null)
            {
                existingLoyaltyPoint = new LoyaltyPoint
                {
                    UserId = user.UserId,
                    Points = pointsEarned,
                    LastUpdated = DateTime.Now
                };
                _context.LoyaltyPoints.Add(existingLoyaltyPoint);
            }
            else
            {
                existingLoyaltyPoint.Points += pointsEarned;
                existingLoyaltyPoint.LastUpdated = DateTime.Now;
                _context.Update(existingLoyaltyPoint);
            }
            await _context.SaveChangesAsync();
            
            TempData["Message"] = $"Ticket purchased successfully! You earned {pointsEarned} loyalty points. Your QR code has been generated.";
            return RedirectToAction("MyTickets");
        }
        
        // My Tickets page
        public async Task<IActionResult> MyTickets()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Users"); // Assume login action
            }
            
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return RedirectToAction("Login", "Users");
            }
            
            var tickets = await _context.Tickets
                .Include(t => t.Event)
                .Where(t => t.UserId == user.UserId)
                .OrderByDescending(t => t.PurchaseDate)
                .ToListAsync();
            
            return View(tickets);
        }

        // GET: Customer/Profile
        public async Task<IActionResult> Profile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Users");
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return RedirectToAction("Login", "Users");
            }

            var model = new EditUserViewModel
            {
                UserId = user.UserId,
                UserName = user.UserName,
                Email = user.Email,
                Role = user.Role,
                IsEdit = true
            };

            return View(model);
        }

        // POST: Customer/Profile
        [HttpPost]
        public async Task<IActionResult> Profile(EditUserViewModel model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Json(new { success = false, message = "Unauthorized." });
            }

            if (model.UserId != userId.Value)
            {
                return Json(new { success = false, message = "Cannot edit other users' profiles." });
            }

            if (ModelState.IsValid)
            {
                var user = await _context.Users.FindAsync(model.UserId);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                // Check if email already exists for other users
                if (await _context.Users.AnyAsync(u => u.Email == model.Email && u.UserId != model.UserId))
                {
                    return Json(new { success = false, message = "Email is already registered to another user." });
                }

                user.UserName = model.UserName;
                user.Email = model.Email;

                // Update password if provided
                if (!string.IsNullOrWhiteSpace(model.Password))
                {
                    user.PasswordHash = HashPassword(model.Password);
                }

                _context.Update(user);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Profile updated successfully." });
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return Json(new { success = false, message = string.Join(", ", errors) });
        }

        // GET: Customer/GetProfile - AJAX method to get profile data
        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Json(new { success = false, message = "Unauthorized." });
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            return Json(new { 
                success = true, 
                userId = user.UserId,
                userName = user.UserName,
                email = user.Email,
                role = user.Role
            });
        }

        // GET: Customer/GetLoyaltyPoints - AJAX method to get loyalty points
        [HttpGet]
        public async Task<IActionResult> GetLoyaltyPoints()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Json(new { success = false, message = "Unauthorized." });
            }

            var loyaltyPoint = await _context.LoyaltyPoints.FindAsync(userId);
            int points = loyaltyPoint?.Points ?? 0;

            return Json(new { success = true, points = points });
        }

        // Helper method to hash password
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        // Generate QR Code as Base64 string
        private string GenerateQRCode(int userId, int eventId)
        {
            string qrText = $"Ticket-{userId}-{eventId}-{Guid.NewGuid()}";
            
            using (var qrGenerator = new QRCodeGenerator())
            {
                var qrCodeData = qrGenerator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new PngByteQRCode(qrCodeData);
                var qrCodeImage = qrCode.GetGraphic(10); // Reduced from 20 to 10 for smaller size
                
                return Convert.ToBase64String(qrCodeImage);
            }
        }
    }
}
