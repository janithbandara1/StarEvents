//using Microsoft.AspNetCore.Mvc;
//using StarEvents.Models;

//namespace StarEvents.Controllers
//{
//    public class TicketsController : BaseController
//    {
//        private readonly StarEventsDbContext _context;
//        public TicketsController(StarEventsDbContext context)
//        {
//            _context = context;
//        }
//        // Actions for ticket booking and purchasing will go here
//    }
//}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StarEvents.Models;
using QRCoder;

namespace StarEvents.Controllers
{
    public class TicketsController : BaseController
    {
        private readonly StarEventsDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public TicketsController(StarEventsDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // GET: Tickets/Purchase/5
        public async Task<IActionResult> Purchase(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Users");
            }

            var eventItem = await _context.Events
                .Include(e => e.Organizer)
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (eventItem == null)
            {
                return NotFound();
            }

            if (eventItem.Status != "Active")
            {
                TempData["Error"] = "This event is no longer available for ticket purchase.";
                return RedirectToAction("Index", "Home");
            }

            return View(eventItem);
        }

        // POST: Tickets/Purchase
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPurchase(int eventId, string paymentMethod)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Json(new { success = false, message = "Please log in to purchase tickets." });
            }

            var eventItem = await _context.Events.FindAsync(eventId);
            if (eventItem == null || eventItem.Status != "Active")
            {
                return Json(new { success = false, message = "Event not available." });
            }

            // Create ticket
            var ticket = new Ticket
            {
                EventId = eventId,
                UserId = userId.Value,
                PurchaseDate = DateTime.Now,
                PricePaid = eventItem.TicketPrice,
                Status = "Valid"
            };

            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync();

            // Generate QR code after ticket is saved (so we have TicketId)
            var qrCodeData = GenerateQRCodeData(ticket.TicketId, eventId, userId.Value);
            ticket.QRCode = qrCodeData;
            _context.Update(ticket);

            // Create payment record
            var payment = new Payment
            {
                TicketId = ticket.TicketId,
                Amount = eventItem.TicketPrice,
                PaymentDate = DateTime.Now,
                PaymentMethod = paymentMethod,
                Status = "Completed"
            };

            _context.Payments.Add(payment);

            // Update loyalty points
            await UpdateLoyaltyPoints(userId.Value, eventItem.TicketPrice);

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Ticket purchased successfully!",
                ticketId = ticket.TicketId
            });
        }

        // GET: Tickets/MyTickets
        public async Task<IActionResult> MyTickets()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Users");
            }

            var tickets = await _context.Tickets
                .Include(t => t.Event)
                .Include(t => t.Payment)
                .Where(t => t.UserId == userId.Value)
                .OrderByDescending(t => t.PurchaseDate)
                .ToListAsync();

            return View(tickets);
        }

        // GET: Tickets/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Users");
            }

            var ticket = await _context.Tickets
                .Include(t => t.Event)
                    .ThenInclude(e => e.Organizer)
                .Include(t => t.Payment)
                .FirstOrDefaultAsync(t => t.TicketId == id && t.UserId == userId.Value);

            if (ticket == null)
            {
                return NotFound();
            }

            return View(ticket);
        }

        // GET: Tickets/GetQRCode/5 - Returns base64 encoded PNG
        public async Task<IActionResult> GetQRCode(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Unauthorized();
            }

            var ticket = await _context.Tickets
                .FirstOrDefaultAsync(t => t.TicketId == id && t.UserId == userId.Value);

            if (ticket == null)
            {
                return NotFound();
            }

            // Generate QR code as base64 string
            var qrCodeBase64 = GenerateQRCodeBase64(ticket.QRCode);

            return Json(new { success = true, qrCode = qrCodeBase64 });
        }

        // GET: Tickets/DownloadQRCode/5
        public async Task<IActionResult> DownloadQRCode(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Unauthorized();
            }

            var ticket = await _context.Tickets
                .Include(t => t.Event)
                .FirstOrDefaultAsync(t => t.TicketId == id && t.UserId == userId.Value);

            if (ticket == null)
            {
                return NotFound();
            }

            // Generate QR code image as PNG bytes
            var qrCodeBytes = GenerateQRCodePng(ticket.QRCode);

            return File(qrCodeBytes, "image/png", $"Ticket-{ticket.TicketId}-QRCode.png");
        }

        // GET: Tickets/Validate
        public IActionResult Validate()
        {
            // Only organizers should validate tickets
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Organizer" && userRole != "Admin")
            {
                return Forbid();
            }

            return View();
        }

        // POST: Tickets/ValidateQRCode
        [HttpPost]
        public async Task<IActionResult> ValidateQRCode([FromBody] ValidateTicketRequest request)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Organizer" && userRole != "Admin")
            {
                return Json(new { success = false, message = "Unauthorized." });
            }

            try
            {
                // Parse QR code data
                var parts = request.QRCodeData.Split('|');
                if (parts.Length != 4 || parts[0] != "STAREVENTS")
                {
                    return Json(new { success = false, message = "Invalid QR code format." });
                }

                var ticketId = int.Parse(parts[1]);
                var eventId = int.Parse(parts[2]);
                var userId = int.Parse(parts[3]);

                // Verify ticket in database
                var ticket = await _context.Tickets
                    .Include(t => t.Event)
                    .Include(t => t.User)
                    .FirstOrDefaultAsync(t => t.TicketId == ticketId
                        && t.EventId == eventId
                        && t.UserId == userId);

                if (ticket == null)
                {
                    return Json(new { success = false, message = "Ticket not found." });
                }

                if (ticket.Status == "Used")
                {
                    return Json(new
                    {
                        success = false,
                        message = "Ticket already used.",
                        ticketInfo = new
                        {
                            ticketId = ticket.TicketId,
                            eventTitle = ticket.Event.Title,
                            userName = ticket.User.UserName,
                            status = ticket.Status
                        }
                    });
                }

                if (ticket.Status == "Cancelled")
                {
                    return Json(new { success = false, message = "Ticket has been cancelled." });
                }

                if (ticket.Event.EventDate.Date > DateTime.Now.Date)
                {
                    return Json(new
                    {
                        success = false,
                        message = "This ticket is for a future event.",
                        ticketInfo = new
                        {
                            ticketId = ticket.TicketId,
                            eventTitle = ticket.Event.Title,
                            eventDate = ticket.Event.EventDate.ToString("yyyy-MM-dd"),
                            userName = ticket.User.UserName
                        }
                    });
                }

                // Mark ticket as used
                ticket.Status = "Used";
                _context.Update(ticket);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Ticket validated successfully!",
                    ticketInfo = new
                    {
                        ticketId = ticket.TicketId,
                        eventTitle = ticket.Event.Title,
                        eventDate = ticket.Event.EventDate.ToString("yyyy-MM-dd HH:mm"),
                        userName = ticket.User.UserName,
                        email = ticket.User.Email,
                        pricePaid = ticket.PricePaid,
                        purchaseDate = ticket.PurchaseDate.ToString("yyyy-MM-dd HH:mm")
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error validating ticket: " + ex.Message });
            }
        }

        // Helper method to generate QR code data string
        private string GenerateQRCodeData(int ticketId, int eventId, int userId)
        {
            // Format: STAREVENTS|TicketId|EventId|UserId
            return $"STAREVENTS|{ticketId}|{eventId}|{userId}";
        }

        // Helper method to generate QR code as base64 string (for display in HTML)
        private string GenerateQRCodeBase64(string qrCodeData)
        {
            using (var qrGenerator = new QRCodeGenerator())
            {
                var qrCodeDataObj = qrGenerator.CreateQrCode(qrCodeData, QRCodeGenerator.ECCLevel.Q);
                using (var qrCode = new PngByteQRCode(qrCodeDataObj))
                {
                    byte[] qrCodeBytes = qrCode.GetGraphic(20);
                    return Convert.ToBase64String(qrCodeBytes);
                }
            }
        }

        // Helper method to generate QR code as PNG bytes (for download)
        private byte[] GenerateQRCodePng(string qrCodeData)
        {
            using (var qrGenerator = new QRCodeGenerator())
            {
                var qrCodeDataObj = qrGenerator.CreateQrCode(qrCodeData, QRCodeGenerator.ECCLevel.Q);
                using (var qrCode = new PngByteQRCode(qrCodeDataObj))
                {
                    return qrCode.GetGraphic(20);
                }
            }
        }

        // Helper method to update loyalty points
        private async Task UpdateLoyaltyPoints(int userId, decimal ticketPrice)
        {
            var loyaltyPoint = await _context.LoyaltyPoints
                .FirstOrDefaultAsync(l => l.UserId == userId);

            // Award 1 point per dollar spent
            int pointsToAdd = (int)Math.Floor(ticketPrice);

            if (loyaltyPoint == null)
            {
                loyaltyPoint = new LoyaltyPoint
                {
                    UserId = userId,
                    Points = pointsToAdd,
                    LastUpdated = DateTime.Now
                };
                _context.LoyaltyPoints.Add(loyaltyPoint);
            }
            else
            {
                loyaltyPoint.Points += pointsToAdd;
                loyaltyPoint.LastUpdated = DateTime.Now;
                _context.Update(loyaltyPoint);
            }
        }
    }

    public class ValidateTicketRequest
    {
        public string QRCodeData { get; set; }
    }
}