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
