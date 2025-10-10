using Microsoft.AspNetCore.Mvc;
using StarEvents.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace StarEvents.Controllers
{
    public class OrganizerController : BaseController
    {
        private readonly StarEventsDbContext _context;
        public OrganizerController(StarEventsDbContext context)
        {
            _context = context;
        }
        public IActionResult Dashboard()
        {
            return View();
        }

        // GET: Organizer/GetDashboardStats - AJAX method to get dashboard statistics
        [HttpGet]
        public async Task<IActionResult> GetDashboardStats()
        {
            var organizerId = HttpContext.Session.GetInt32("UserId");
            if (organizerId == null)
            {
                return Json(new { success = false, message = "Unauthorized." });
            }

            var totalEvents = await _context.Events.CountAsync(e => e.OrganizerId == organizerId.Value);
            var activeEvents = await _context.Events.CountAsync(e => e.OrganizerId == organizerId.Value && e.Status == "Active");
            var totalTicketsSold = await _context.Tickets.CountAsync(t => t.Event.OrganizerId == organizerId.Value);
            var totalRevenue = await _context.Tickets.Where(t => t.Event.OrganizerId == organizerId.Value).SumAsync(t => t.PricePaid);

            return Json(new { 
                success = true, 
                data = new {
                    totalEvents,
                    activeEvents,
                    totalTicketsSold,
                    totalRevenue
                }
            });
        }

        // GET: Organizer/Profile
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

        // POST: Organizer/Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(EditUserViewModel model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Users");
            }

            if (model.UserId != userId.Value)
            {
                return Forbid(); // Cannot edit other users' profiles
            }

            if (ModelState.IsValid)
            {
                var user = await _context.Users.FindAsync(model.UserId);
                if (user == null)
                {
                    return NotFound();
                }

                // Check if email already exists for other users
                if (await _context.Users.AnyAsync(u => u.Email == model.Email && u.UserId != model.UserId))
                {
                    ModelState.AddModelError("Email", "Email is already registered to another user.");
                    return View(model);
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

                TempData["SuccessMessage"] = "Profile updated successfully.";
                return RedirectToAction("Profile");
            }

            return View(model);
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

        // GET: Organizer/GetSalesReport - AJAX method to get sales report data
        [HttpGet]
        public async Task<IActionResult> GetSalesReport()
        {
            var organizerId = HttpContext.Session.GetInt32("UserId");
            if (organizerId == null)
            {
                return Json(new { success = false, message = "Unauthorized." });
            }

            var salesReport = await _context.Events
                .Where(e => e.OrganizerId == organizerId.Value)
                .Select(e => new
                {
                    EventTitle = e.Title,
                    TicketsSold = e.Tickets.Count(),
                    TotalSales = e.Tickets.Sum(t => t.PricePaid)
                })
                .ToListAsync();

            return Json(new { success = true, data = salesReport });
        }
    }
}
