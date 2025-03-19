using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using os.Models; // Replace with your actual namespace
using os.Areas.Identity.Data;
using System.Threading.Tasks;

namespace os.Controllers
{
    public class HomeController : Controller
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;

        public HomeController(SignInManager<AppUser> signInManager, UserManager<AppUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Meetings()
        {
            return View();
        }

        [HttpGet]
        public IActionResult GeneralServiceOffice()
        {
            return View();
        }

        [HttpGet]
        public IActionResult AtlantaMeetingSchedule()
        {
            return View();
        }

        [HttpGet]
        public IActionResult EverythingAAApp()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewBag.StatusMessage = "Email and password are required.";
                return View();
            }

            // Normalize the email and find the user
            var normalizedEmail = _userManager.NormalizeEmail(email);
            var user = await _userManager.FindByEmailAsync(normalizedEmail);

            if (user != null)
            {
                // Attempt to sign in the user
                var result = await _signInManager.PasswordSignInAsync(user, password, isPersistent: false, lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    // Login successful
                    return RedirectToAction("Index", "Member");
                }
                else
                {
                    // Login failed
                    ViewBag.StatusMessage = "Invalid login attempt.";
                }
            }
            else
            {
                // User not found
                ViewBag.StatusMessage = "User not found.";
            }

            // If we got this far, something failed; redisplay the form
            return View();
        }

            public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult FirstLogin()
        {
            return View();
        }

        //[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        //public IActionResult Error()
        //{
        //    return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        //}
    }
}
