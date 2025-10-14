using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StarEvents.Models;

namespace StarEvents.Controllers
{
    public class EventsController : BaseController
    {
        private readonly StarEventsDbContext _context;
        
        public EventsController(StarEventsDbContext context)
        {
            _context = context;
        }

        // GET: Events
        public async Task<IActionResult> Index()
        {
            var events = await _context.Events
                .Include(e => e.Organizer)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();
            return View(events);
        }

        // GET: Events/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Events/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] CreateEventViewModel model)
        {
            if (ModelState.IsValid)
            {
                var organizerId = HttpContext.Session.GetInt32("UserId");
                if (organizerId == null)
                {
                    return RedirectToAction("Login", "Users");
                }

                var newEvent = new Event
                {
                    OrganizerId = organizerId.Value,
                    Title = model.Title,
                    Description = model.Description,
                    Category = model.Category,
                    Location = model.Location,
                    EventDate = model.EventDate,
                    TicketPrice = model.TicketPrice,
                    Status = model.Status,
                    CreatedAt = DateTime.Now
                };

                _context.Events.Add(newEvent);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Event created successfully!" });
            }

            return Json(new { success = false, message = "Please fill in all required fields." });
        }

        // GET: Events/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var eventItem = await _context.Events.FindAsync(id);
            if (eventItem == null)
            {
                return NotFound();
            }

            // Check if current user is the organizer of this event
            var organizerId = HttpContext.Session.GetInt32("UserId");
            if (organizerId != eventItem.OrganizerId)
            {
                return Forbid();
            }

            return View(eventItem);
        }

        // POST: Events/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [FromForm] EditEventViewModel model)
        {
            if (ModelState.IsValid)
            {
                var eventItem = await _context.Events.FindAsync(id);
                if (eventItem == null)
                {
                    return Json(new { success = false, message = "Event not found." });
                }

                // Check if current user is the organizer of this event
                var organizerId = HttpContext.Session.GetInt32("UserId");
                if (organizerId != eventItem.OrganizerId)
                {
                    return Json(new { success = false, message = "Unauthorized." });
                }

                eventItem.Title = model.Title;
                eventItem.Description = model.Description;
                eventItem.Category = model.Category;
                eventItem.Location = model.Location;
                eventItem.EventDate = model.EventDate;
                eventItem.TicketPrice = model.TicketPrice;
                eventItem.Status = model.Status;

                _context.Update(eventItem);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Event updated successfully!" });
            }

            return Json(new { success = false, message = "Please fill in all required fields." });
        }

        // DELETE: Events/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var eventItem = await _context.Events.FindAsync(id);
            if (eventItem == null)
            {
                return Json(new { success = false, message = "Event not found." });
            }

            // Check if current user is the organizer of this event
            var organizerId = HttpContext.Session.GetInt32("UserId");
            if (organizerId != eventItem.OrganizerId)
            {
                return Json(new { success = false, message = "Unauthorized." });
            }

            // Check if the event has any sold tickets
            if (await _context.Tickets.AnyAsync(t => t.EventId == id))
            {
                return Json(new { success = false, message = "Cannot delete event with sold tickets." });
            }

            _context.Events.Remove(eventItem);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Event deleted successfully!" });
        }

        // API: Get events for current organizer
        [HttpGet]
        public async Task<IActionResult> GetEvents()
        {
            var organizerId = HttpContext.Session.GetInt32("UserId");
            if (organizerId == null)
            {
                return Json(new { success = false, message = "Unauthorized." });
            }

            var events = await _context.Events
                .Where(e => e.OrganizerId == organizerId.Value)
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new
                {
                    e.EventId,
                    e.Title,
                    EventDate = e.EventDate.ToString("yyyy-MM-dd"),
                    e.Location,
                    e.TicketPrice,
                    e.Status
                })
                .ToListAsync();

            return Json(new { success = true, data = events });
        }

        // API: Get single event for editing
        [HttpGet]
        public async Task<IActionResult> GetEvent(int id)
        {
            var organizerId = HttpContext.Session.GetInt32("UserId");
            if (organizerId == null)
            {
                return Json(new { success = false, message = "Unauthorized." });
            }

            var eventItem = await _context.Events
                .Where(e => e.EventId == id && e.OrganizerId == organizerId.Value)
                .Select(e => new
                {
                    e.EventId,
                    e.Title,
                    e.Description,
                    e.Category,
                    e.Location,
                    EventDate = e.EventDate.ToString("yyyy-MM-dd"),
                    e.TicketPrice,
                    e.Status
                })
                .FirstOrDefaultAsync();

            if (eventItem == null)
            {
                return Json(new { success = false, message = "Event not found." });
            }

            return Json(new { success = true, data = eventItem });
        }
    }
}
