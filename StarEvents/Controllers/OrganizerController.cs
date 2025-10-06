using Microsoft.AspNetCore.Mvc;
using StarEvents.Models;

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
    }
}
