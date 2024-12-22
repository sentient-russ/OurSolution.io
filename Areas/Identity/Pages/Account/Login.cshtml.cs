#nullable disable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using os.Areas.Identity.Data;
using os.Services;

namespace os.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly UserManager<AppUser> _userManager;
        private readonly DbConnectionService _dbConnectionService;

        public LoginModel(SignInManager<AppUser> signInManager, ILogger<LoginModel> logger, UserManager<AppUser> userManager, DbConnectionService dbConnectionService)
        {
            _signInManager = signInManager;
            _logger = logger;
            _userManager = userManager;
            _dbConnectionService = dbConnectionService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [Display(Name = "Email:")]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [Display(Name = "Password:")]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");


            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            // gets the user by email for role based redirect
            //var user = await _userManager.FindByEmailAsync(Input.Email);
            var user = _dbConnectionService.GetUserDetailsByEmail(Input.Email);

            if (user.ActiveStatus == "Disabled")
            {
                _logger.LogWarning("User account locked out.");
                return RedirectToPage("./Lockout");
            }

            if (ModelState.IsValid)
            {
                // To enable password failures to trigger account lockout, set lockoutOnFailure: true
                var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User logged in.");
                    // directs new unaproved accounts to a "Awaiting confirmation" message if their acount has not been approved.
                    if (user.UserRole == null || user.UserRole == "")
                    {
                        returnUrl += "Home/FirstLogin";
                        _dbConnectionService.CreateLog(user.Email, "Unapproved account login attempted.", "No Change", "No Change");
                        return LocalRedirect(returnUrl);
                    }
                    // redirects users that have the Manager or Accountant roles to the accounting application
                    if (user.UserRole != null && user.UserRole != "")
                    {
                        if (user.UserRole == "Member")
                        {
                            returnUrl += "Member/Index";
                            return LocalRedirect(returnUrl); 
                        }
                        else if (user.UserRole == "Administrator")
                        {
                            returnUrl += "Admin/Index";
                            return LocalRedirect(returnUrl); 
                        } else
                        {
                            returnUrl += "Home/Index";
                            return LocalRedirect(returnUrl);
                        }
                    }
                    return LocalRedirect(returnUrl);
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }
            }
            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}
