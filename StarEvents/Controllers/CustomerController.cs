using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StarEvents.Models;

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
    }
}
