using Microsoft.AspNetCore.Mvc;
using StarEvents.Models;

namespace StarEvents.Controllers
{
    public class TicketsController : Controller
    {
        private readonly StarEventsDbContext _context;
        public TicketsController(StarEventsDbContext context)
        {
            _context = context;
        }
        // Actions for ticket booking and purchasing will go here
    }
}
