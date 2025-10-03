using Microsoft.AspNetCore.Mvc;
using StarEvents.Models;

namespace StarEvents.Controllers
{
    public class AdminController : Controller
    {
        private readonly StarEventsDbContext _context;
        public AdminController(StarEventsDbContext context)
        {
            _context = context;
        }
        // Actions for admin dashboard, user/event/ticket management, and reports will go here
    }
}
