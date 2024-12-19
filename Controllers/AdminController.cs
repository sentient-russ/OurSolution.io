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
        public async Task<IActionResult> UploadSpeaker([Bind("SpeakerId, FileName, FirstName, LastName, Description, NumUpVotes, DateRecorded, UploadDate, UploadedBy, SpeakerStatus, FormFile")] SpeakerModel newSpeakerIn)
        {
            if (newSpeakerIn.FormFile == null || newSpeakerIn.FormFile.Length == 0)
            {
                ViewBag.StatusMessage = "No file selected.";
                return View("UploadSpeaker");
            }

            const long max_size = 50 * 1024 * 1024;
            if (newSpeakerIn.FormFile.Length > max_size)
            {
                ViewBag.StatusMessage = "The file size must be less than 50MB.";
                return View("UploadSpeaker");
            }

            var fileExtension = Path.GetExtension(newSpeakerIn.FormFile.FileName).ToLowerInvariant();
            if (fileExtension != ".mp3")
            {
                ViewBag.StatusMessage = "The file type must be a .mp3 type.";
                return View("UploadSpeaker");
            }

            DateTimeOffset current_time = DateTimeOffset.UtcNow;
            long ms_time = current_time.ToUnixTimeMilliseconds();
            string new_file_name = ms_time.ToString() + ".mp3";

            var filePath = Path.Combine("wwwroot", "uploads", new_file_name);
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await newSpeakerIn.FormFile.CopyToAsync(stream);
                }
            }
            catch (Exception ex)
            {
                ViewBag.StatusMessage = "Error uploading file: " + ex.Message;
                return View("UploadSpeaker");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                newSpeakerIn.FileName = new_file_name;
                newSpeakerIn.UploadedBy = user.Id;

                var succeeded = _dbConnectionService.AddSpeaker(newSpeakerIn);
                if (succeeded)
                {
                    ViewBag.StatusMessage = "File uploaded successfully.";
                    return RedirectToAction("Index");
                }
                else
                {
                    ViewBag.StatusMessage = "Error uploading. Try again or contact development.";
                    return View("UploadSpeaker");
                }
            }
            else
            {
                ViewBag.StatusMessage = "User not found.";
                return View("UploadSpeaker");
            }
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

        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> ViewSpeakers()
        {
            List<SpeakerModel> speakersList = new List<SpeakerModel>();
            speakersList = _dbConnectionService.GetSpeakersList();
            speakersList.Reverse();
            return View(speakersList);
        }

        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> AddSpeaker()
        {
            SpeakerModel speaker = new SpeakerModel();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            AppUser userDetails = _dbConnectionService.GetUserDetailsById(userId);
            speaker.UploadedBy = userDetails.Email;
            return View(speaker);
        }
    }
}