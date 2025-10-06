using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace StarEvents.Controllers
{
    public class BaseController : Controller
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // Redirect to Login if not authenticated and not already on Login/Register
            var userId = HttpContext.Session.GetInt32("UserId");
            var controller = context.RouteData.Values["controller"]?.ToString();
            var action = context.RouteData.Values["action"]?.ToString();
            if (userId == null && !(controller == "Users" && (action == "Login" || action == "Register")))
            {
                context.Result = RedirectToAction("Login", "Users");
            }
            base.OnActionExecuting(context);
        }
    }
}
