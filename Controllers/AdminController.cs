using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using os.Areas.Identity.Data;
using os.Models;
using os.Services;
using System.Security.Claims;

namespace os.Controllers
{
    [BindProperties(SupportsGet = true)]
    public class AdminController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly DbConnectionService _dbConnectionService;
        private readonly ApplicationDbContext _DbContext;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(UserManager<AppUser> userManager, 
            DbConnectionService dbConnectionService, 
            ApplicationDbContext DbContext, 
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _dbConnectionService = dbConnectionService;
            _DbContext = DbContext;
            _roleManager = roleManager;
        }

        [Authorize(Roles = "Administrator")]
        public IActionResult Index()
        {
            //leaving this as example on obtaining users details from identity claims.
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            AppUser userDetails = _dbConnectionService.GetUserDetailsById(userId);
            return View();
        }

        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> ManageAccounts()
        {
            var appUsers = await _DbContext.Users.ToListAsync();
            return View(appUsers);
        }

        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> EditAccountDetails(string? Id)
        {
            var appUsers = await _DbContext.Users.ToListAsync();
            AppUser appUser = new AppUser();

            for (var i = 0; i < appUsers.Count; i++)
            {
                if (appUsers[i].Id == Id)
                {
                    appUser = appUsers[i];
                }
            }
            var roles = await _roleManager.Roles.ToListAsync();
            var items = new List<SelectListItem>();
            foreach (var role in roles)
            {
                items.Add(new SelectListItem
                {
                    Text = role.Name,
                });
            }
            appUser.RoleList = items;
            return View(appUser);
        }
       /*
        * Update the account details.  Admin access only
        */
        [Authorize(Roles = "Administrator")]
        [HttpPost]
        public async Task<IActionResult> UpdateAccountDetails([Bind("Id, FirstName, LastName, PhoneNumber, BellyButtonBirthday, AABirthday, Address, City, State, Zip, UserRole, ActiveStatus, ProfileImage, Email, NormalizedUserName, AcctSuspensionDate, AcctReinstatementDate, LastPasswordChangedDate, PasswordResetDays, ")] AppUser detailsIn)
        {
            AppUser appUser = new AppUser();
            appUser = detailsIn;
            _dbConnectionService.UpdateUserDetailsAsync(appUser);
            var user = await _userManager.FindByIdAsync(appUser.Id);

            if (user != null)
            {
                var roleResult = await _userManager.AddToRoleAsync(user, appUser.UserRole);
            }
            return RedirectToAction(nameof(ManageAccounts));
        }
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> UploadSpeaker([Bind("SpeakerId, FileName, FirstName, LastName, Description, NumUpVotes, DateRecorded, UploadDate, UploadedBy, FormFile")] SpeakerModel newSpeakerIn)
        {
            if (newSpeakerIn.FormFile != null && newSpeakerIn.FormFile.Length > 0)
            {
                // Check file length
                const long max_size = 50 * 1024 * 1024;
                if (newSpeakerIn.FormFile.Length > max_size)
                {
                    ViewBag.StatusMessage = "The file size must be less than 50MB.";
                    return View("UploadSpeaker");
                }

                // Check file type extension
                var fileExtension = Path.GetExtension(newSpeakerIn.FormFile.FileName).ToLowerInvariant();
                if (fileExtension != ".mp3")
                {
                    ViewBag.StatusMessage = "The file type must be a .mp3 type.";
                    return View("UploadSpeaker");
                }

                // Create a new file name for security purposes
                DateTimeOffset current_time = DateTimeOffset.UtcNow;
                long ms_time = current_time.ToUnixTimeMilliseconds();
                string new_file_name = ms_time.ToString() + ".mp3";

                // Save the data stream to a file
                var filePath = Path.Combine("wwwroot", "uploads", new_file_name);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await newSpeakerIn.FormFile.CopyToAsync(stream);
                }

                // Save the new file name to the user's DB record
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    // swap the file name in the newSpeakerIn object with the new file name
                    newSpeakerIn.FileName = new_file_name;
                    // update the user's ID in the newSpeakerIn object
                    newSpeakerIn.UploadedBy = user.Id;
                    // save the newSpeakerIn object to the database
                    var addSpeakerResult = _dbConnectionService.AddSpeaker(newSpeakerIn);
                    var succeeded = _dbConnectionService.AddSpeaker(newSpeakerIn);

                    if (succeeded)
                    {
                        ViewBag.StatusMessage = "File uploaded successfully.";
                    }
                    else
                    {
                        ViewBag.StatusMessage = "Error uploading. Try again or contact development.";
                    }
                }
                else
                {
                    ViewBag.StatusMessage = "User not found.";
                }
            }
            else
            {
                ViewBag.StatusMessage = "No file selected.";
            }
            return View("Index");
        }
        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> ViewLogs()
        {
            List<LogModel> logs = new List<LogModel>();
            logs = _dbConnectionService.GetLogs();
            logs.Reverse();
            return View(logs);
        }
    }
}