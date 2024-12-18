using Microsoft.AspNetCore.Mvc;

namespace os.Controllers
{
    public class MemberController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
