#nullable disable
// This class is used to prevent the admin account password from being able to be reset.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace os.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ForgotPasswordAdminAccount : PageModel
    {
        public void OnGet()
        {
        }
    }
}
