using Microsoft.AspNetCore.Mvc;
using StarEvents.Models;

namespace StarEvents.Controllers
{
    public class EventsController : Controller
    {
        private readonly StarEventsDbContext _context;
        public EventsController(StarEventsDbContext context)
        {
            _context = context;
        }
        // Actions for event CRUD (organizer) will go here
    }
}
