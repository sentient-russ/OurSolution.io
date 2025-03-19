using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using os.Services;

namespace os.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class StatsController : Controller
    {
        private readonly StatsTracker _userTracker;

        public StatsController(StatsTracker userTracker)
        {
            _userTracker = userTracker;
        }
        [Authorize(Roles = "Administrator")]
        public IActionResult Index()
        {
            var activeUsers = _userTracker.GetActiveUsers(TimeSpan.FromMinutes(5));
            ViewBag.UserCount = activeUsers.Count;
            return View(activeUsers);
        }
    }
}