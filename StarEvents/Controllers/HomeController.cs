using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StarEvents.Models;

namespace StarEvents.Controllers
{
    public class HomeController : BaseController
    {
        private readonly ILogger<HomeController> _logger;
        private readonly StarEventsDbContext _context;

        public HomeController(ILogger<HomeController> logger, StarEventsDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Get upcoming events for the public home page
            var upcomingEvents = await _context.Events
                .Include(e => e.Organizer)
                .Where(e => e.Status == "Active" && e.EventDate >= DateTime.Now)
                .OrderBy(e => e.EventDate)
                .Take(6) // Show only 6 featured events on home page
                .ToListAsync();
                
            return View(upcomingEvents);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
