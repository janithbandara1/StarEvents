using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StarEvents.Models;
using Stripe.Checkout;
using QRCoder;
using System.Drawing.Imaging;

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
        public async Task<IActionResult> CreateCheckoutSession(int eventId)
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
            
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(eventItem.TicketPrice * 100), // Amount in cents
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
                SuccessUrl = Url.Action("PurchaseSuccess", "Customer", new { eventId = eventId }, Request.Scheme),
                CancelUrl = Url.Action("Dashboard", "Customer", null, Request.Scheme),
                Metadata = new Dictionary<string, string>
                {
                    { "eventId", eventId.ToString() },
                    { "userId", user.UserId.ToString() }
                }
            };
            
            var service = new SessionService();
            Session session = service.Create(options);
            
            return Json(new { success = true, url = session.Url });
        }
        
        // Handle successful payment
        [HttpGet]
        public async Task<IActionResult> PurchaseSuccess(int eventId)
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
                PricePaid = eventItem.TicketPrice,
                Status = "Purchased",
                QRCode = qrCodeBase64
            };
            
            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync();
            
            // Create payment record
            var payment = new Payment
            {
                TicketId = ticket.TicketId,
                Amount = eventItem.TicketPrice,
                PaymentDate = DateTime.Now,
                PaymentMethod = "Stripe",
                Status = "Completed"
            };
            
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
            
            TempData["Message"] = "Ticket purchased successfully! Your QR code has been generated.";
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
