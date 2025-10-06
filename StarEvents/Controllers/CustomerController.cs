using Microsoft.AspNetCore.Mvc;
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
        public IActionResult Dashboard()
        {
            return View();
        }
    }
}
