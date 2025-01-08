// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using os.Areas.Identity.Data;
using os.Models;
using os.Services;

namespace os.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly IUserStore<AppUser> _userStore;
        private readonly IUserEmailStore<AppUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly DbConnectionService _dbConnectionService;

        public RegisterModel(
            UserManager<AppUser> userManager,
            IUserStore<AppUser> userStore,
            SignInManager<AppUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            DbConnectionService dbConnectionService)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _dbConnectionService = dbConnectionService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {

            static DateTime today = System.DateTime.Now;

            [Required]
            [EmailAddress]
            [Display(Name = "Email:")]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            [Required]
            //[StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {100} characters long.", MinimumLength = 2)]
            [Display(Name = "First Name")]
            public string? FirstName { get; set; } = "";

            [StringLength(100, MinimumLength = 1)]
            [Required]
            [Display(Name = "Last Name")]
            public string? LastName { get; set; } = "";

            //[StringLength(100, ErrorMessage = "The phone number must be 9 digets long.", MinimumLength = 11)]
            [Required]
            [Display(Name = "Phone Number")]
            public string? PhoneNumber { get; set; } = "";

            [Display(Name = "Belly Button Birthday")]
            public DateTime? BellyButtonBirthday { get; set; } = today;

            [Display(Name = "AA Birthday")]
            public DateTime? AABirthday { get; set; } = today;

            [Display(Name = "Address Line 1")]
            public string? Address { get; set; } = "";

            [Display(Name = "City")]
            public string? City { get; set; } = "";

            [Display(Name = "State")]
            public string? State { get; set; } = "";

            [Display(Name = "Zip Code")]
            public string? Zip { get; set; } = "";

            [Display(Name = "User Role")]
            public string? UserRole { get; set; } = "Member";

            [Display(Name = "User Status")]
            public bool? ActiveStatus { get; set; } = true;
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            if (ModelState.IsValid)
            {
                var user = CreateUser();
                user.Email = Input.Email;
                user.FirstName = Input.FirstName;
                user.LastName = Input.LastName;
                user.PhoneNumber = Input.PhoneNumber;
                user.Address = Input.Address;
                user.City = Input.City;
                user.State = Input.State;
                user.Zip = Input.Zip;
                user.BellyButtonBirthday = Input.BellyButtonBirthday;
                user.AABirthday = Input.AABirthday;
                user.NormalizedEmail = Input.Email.ToUpper().Trim();
                user.NormalizedUserName = Input.Email.ToUpper().Trim();
                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    //pass the user model to the db access save details function with will append the users role to the junction table
                    AppUser newUserReturnedModel = _dbConnectionService.GetUserDetailsByEmail(user.Email);
                    _dbConnectionService.UpdateUserDetailsAsync(newUserReturnedModel);

                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
                        $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                    // Send an email to the admin to approve the account
                    List<RoleModel> adminRolesList = _dbConnectionService.GetUserRole("Administrator");
                    string adminEmailSubject = $"Account approval needed - OurSolution.io (Sponsored by MagnaDigi.com) {user.FirstName} {user.LastName}";
                    for (var i = 0; i < adminRolesList.Count; i++)
                    {
                        string adminEmailBody = $"Dear {adminRolesList[i].firstName} {adminRolesList[i].lastName} please unlock and assign a role to the user: {user.FirstName}, email: {user.Email}";
                        await _emailSender.SendEmailAsync(adminRolesList[i].email, adminEmailSubject, adminEmailBody);
                    }

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            // If we got this far, something failed, redisplay form
            return Page();
        }

        private AppUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<AppUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(AppUser)}'. " +
                    $"Ensure that '{nameof(AppUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<AppUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<AppUser>)_userStore;
        }
    }
}
