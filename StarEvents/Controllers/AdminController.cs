using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StarEvents.Models;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace StarEvents.Controllers
{
    public class AdminController : BaseController
    {
        private readonly StarEventsDbContext _context;
        public AdminController(StarEventsDbContext context)
        {
            _context = context;
        }

        // Actions for admin dashboard, user/event/ticket management, and reports will go here
        public IActionResult Dashboard()
        {
            var users = _context.Users.ToList();
            ViewBag.Users = users;
            return View();
        }

        // GET: Admin/GetDashboardStats - AJAX method to get dashboard statistics
        [HttpGet]
        public async Task<IActionResult> GetDashboardStats()
        {
            var totalUsers = await _context.Users.CountAsync();
            var totalEvents = await _context.Events.CountAsync();
            var activeEvents = await _context.Events.CountAsync(e => e.Status == "Active");
            var totalTicketsSold = await _context.Tickets.CountAsync();
            var totalRevenue = await _context.Tickets.SumAsync(t => t.PricePaid);

            return Json(new { 
                success = true, 
                data = new {
                    totalUsers,
                    totalEvents,
                    activeEvents,
                    totalTicketsSold,
                    totalRevenue
                }
            });
        }

        // GET: Admin/Profile
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

        // POST: Admin/Profile
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

        // Users action for dedicated users page
        public IActionResult Users()
        {
            var users = _context.Users.ToList();
            return View(users);
        }

        // GET: Admin/GetUser/5 - AJAX method to get user details for modal
        [HttpGet]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var model = new EditUserViewModel
            {
                UserId = user.UserId,
                UserName = user.UserName,
                Email = user.Email,
                Role = user.Role,
                IsEdit = true
            };

            return Json(model);
        }

        // POST: Admin/SaveUser - AJAX method to save user (add or edit)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveUser(EditUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    if (model.UserId == 0) // Add new user
                    {
                        // Check if email already exists
                        if (await _context.Users.AnyAsync(u => u.Email == model.Email))
                        {
                            return Json(new { success = false, message = "Email is already registered." });
                        }

                        // Password is required for new users
                        if (string.IsNullOrWhiteSpace(model.Password))
                        {
                            return Json(new { success = false, message = "Password is required for new users." });
                        }

                        var newUser = new User
                        {
                            UserName = model.UserName,
                            Email = model.Email,
                            Role = model.Role,
                            PasswordHash = HashPassword(model.Password),
                            CreatedAt = DateTime.Now
                        };

                        _context.Users.Add(newUser);
                        await _context.SaveChangesAsync();

                        // Create loyalty points for customer
                        if (model.Role == "Customer")
                        {
                            var loyaltyPoint = new LoyaltyPoint
                            {
                                UserId = newUser.UserId,
                                Points = 0,
                                LastUpdated = DateTime.Now
                            };
                            _context.LoyaltyPoints.Add(loyaltyPoint);
                            await _context.SaveChangesAsync();
                        }

                        return Json(new { success = true, message = "User created successfully." });
                    }
                    else // Edit existing user
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
                        user.Role = model.Role;

                        // Update password if provided
                        if (!string.IsNullOrWhiteSpace(model.Password))
                        {
                            user.PasswordHash = HashPassword(model.Password);
                        }

                        _context.Update(user);
                        await _context.SaveChangesAsync();

                        return Json(new { success = true, message = "User updated successfully." });
                    }
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = "An error occurred while saving the user. Please try again." });
                }
            }

            // Return validation errors
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return Json(new { success = false, message = string.Join(", ", errors) });
        }

        // POST: Admin/DeleteUser/5 - AJAX method to delete user
        [HttpPost]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                // You might want to add additional checks here before deleting
                // For example, check if user has events, tickets, etc.

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "User deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while deleting the user. Please try again." });
            }
        }

        // GET: Admin/GetSalesReport - AJAX method to get sales report data
        [HttpGet]
        public async Task<IActionResult> GetSalesReport()
        {
            var salesReport = await _context.Events
                .Select(e => new
                {
                    EventTitle = e.Title,
                    TicketsSold = e.Tickets.Count(),
                    TotalSales = e.Tickets.Sum(t => t.PricePaid)
                })
                .ToListAsync();

            return Json(new { success = true, data = salesReport });
        }

        // GET: Admin/GetUsersReport - AJAX method to get users report data
        [HttpGet]
        public async Task<IActionResult> GetUsersReport()
        {
            var usersReport = await _context.Users
                .Select(u => new
                {
                    UserName = u.UserName,
                    Email = u.Email,
                    Role = u.Role,
                    EventsOrganized = u.Events.Count(),
                    TicketsPurchased = u.Tickets.Count()
                })
                .ToListAsync();

            return Json(new { success = true, data = usersReport });
        }

        // GET: Admin/GetEventsReport - AJAX method to get events report data
        [HttpGet]
        public async Task<IActionResult> GetEventsReport()
        {
            try
            {
                var eventsReport = await _context.Events
                    .Include(e => e.Organizer)
                    .Select(e => new
                    {
                        Title = e.Title ?? "Untitled",
                        EventDate = e.EventDate.ToString("yyyy-MM-dd"),
                        Location = e.Location ?? "Unknown",
                        OrganizerName = e.Organizer != null ? e.Organizer.UserName : "Unknown",
                        TicketsSold = e.Tickets.Count()
                    })
                    .ToListAsync();

                return Json(new { success = true, data = eventsReport });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while loading events report." });
            }
        }

        // GET: Admin/GetDiscounts - AJAX method to get discounts for table
        [HttpGet]
        public async Task<IActionResult> GetDiscounts()
        {
            var discounts = await _context.Discounts
                .OrderBy(d => d.Code)
                .Select(d => new
                {
                    d.DiscountId,
                    d.Code,
                    d.Description,
                    d.Percentage,
                    ValidFrom = d.ValidFrom.HasValue ? d.ValidFrom.Value.ToString("yyyy-MM-dd") : null,
                    ValidTo = d.ValidTo.HasValue ? d.ValidTo.Value.ToString("yyyy-MM-dd") : null,
                    d.IsActive
                })
                .ToListAsync();

            return Json(new { success = true, data = discounts });
        }

        // POST: Admin/SaveDiscount - AJAX method to save discount
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDiscount(int discountId, string code, string description, int percentage, string validFrom, string validTo, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return Json(new { success = false, message = "Discount code is required." });
            }

            if (percentage < 1 || percentage > 100)
            {
                return Json(new { success = false, message = "Percentage must be between 1 and 100." });
            }

            DateTime? validFromDate = string.IsNullOrWhiteSpace(validFrom) ? null : DateTime.Parse(validFrom);
            DateTime? validToDate = string.IsNullOrWhiteSpace(validTo) ? null : DateTime.Parse(validTo);

            try
            {
                if (discountId == 0) // Add new
                {
                    if (await _context.Discounts.AnyAsync(d => d.Code == code))
                    {
                        return Json(new { success = false, message = "Discount code already exists." });
                    }

                    var discount = new Discount
                    {
                        Code = code,
                        Description = description,
                        Percentage = percentage,
                        ValidFrom = validFromDate,
                        ValidTo = validToDate,
                        IsActive = isActive
                    };

                    _context.Discounts.Add(discount);
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, message = "Discount created successfully." });
                }
                else // Edit existing
                {
                    var discount = await _context.Discounts.FindAsync(discountId);
                    if (discount == null)
                    {
                        return Json(new { success = false, message = "Discount not found." });
                    }

                    if (await _context.Discounts.AnyAsync(d => d.Code == code && d.DiscountId != discountId))
                    {
                        return Json(new { success = false, message = "Discount code already exists." });
                    }

                    discount.Code = code;
                    discount.Description = description;
                    discount.Percentage = percentage;
                    discount.ValidFrom = validFromDate;
                    discount.ValidTo = validToDate;
                    discount.IsActive = isActive;

                    _context.Update(discount);
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, message = "Discount updated successfully." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while saving the discount." });
            }
        }

        // POST: Admin/DeleteDiscount - AJAX method to delete discount
        [HttpPost]
        public async Task<IActionResult> DeleteDiscount(int id)
        {
            try
            {
                var discount = await _context.Discounts.FindAsync(id);
                if (discount == null)
                {
                    return Json(new { success = false, message = "Discount not found." });
                }

                _context.Discounts.Remove(discount);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Discount deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while deleting the discount." });
            }
        }

        // GET: Admin/GetDiscount/5 - AJAX method to get discount details
        [HttpGet]
        public async Task<IActionResult> GetDiscount(int id)
        {
            var discount = await _context.Discounts.FindAsync(id);
            if (discount == null)
            {
                return NotFound();
            }

            return Json(new {
                discountId = discount.DiscountId,
                code = discount.Code,
                description = discount.Description,
                percentage = discount.Percentage,
                validFrom = discount.ValidFrom?.ToString("yyyy-MM-dd"),
                validTo = discount.ValidTo?.ToString("yyyy-MM-dd"),
                isActive = discount.IsActive
            });
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
    }
}
