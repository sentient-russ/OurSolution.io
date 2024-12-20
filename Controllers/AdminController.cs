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
        [RequestSizeLimit(300 * 1024 * 1024)] // 200MB        
        public async Task<IActionResult> UploadSpeaker([Bind("SpeakerId, FileName, FirstName, LastName, Description, NumUpVotes, DateRecorded, UploadDate, UploadedBy, SpeakerStatus, FormFile")] SpeakerModel newSpeakerIn)
        {
            var LoggedInAppUser = await _userManager.GetUserAsync(User);

            // reformat names of the speaker and uploading user to capital first name letter followed by lowercase and the same for the speaker.
            string speakerFirstNameInitial = newSpeakerIn.FirstName.Substring(0, 1).ToUpper();
            string speakerFirstNamePostFix = newSpeakerIn.FirstName.Substring(1).ToLower();
            string speakerFirstName = speakerFirstNameInitial + speakerFirstNamePostFix;
            newSpeakerIn.FirstName = speakerFirstName;
            string speakerLastNameInitial = newSpeakerIn.LastName.Substring(0, 1).ToUpper();
            string speakerLastNamePostFix = newSpeakerIn.LastName.Substring(1).ToLower();
            string speakerLastName = speakerLastNameInitial + speakerLastNamePostFix;
            newSpeakerIn.LastName = speakerLastName;
            string? speakerAbbreviation = newSpeakerIn.FirstName + "_" + newSpeakerIn.LastName.Substring(0, 1).ToUpper();
            string? LoggedInUserAbbreviation = LoggedInAppUser.FirstName + "_" + LoggedInAppUser.LastName.Substring(0, 1).ToUpper();

            if (newSpeakerIn.FormFile == null || newSpeakerIn.FormFile.Length == 0)
            {
                ViewBag.StatusMessage = "No file selected.";
                return View("AddSpeaker");
            }

            const long max_size = 200 * 1024 * 1024;
            if (newSpeakerIn.FormFile.Length > max_size)
            {
                ViewBag.StatusMessage = $"The file size must be less than 200MB. THe current file is {newSpeakerIn.FormFile.Length.ToString()}";
                return View("AddSpeaker");
            }

            var fileExtension = Path.GetExtension(newSpeakerIn.FormFile.FileName).ToLowerInvariant();
            if (fileExtension != ".mp3")
            {
                ViewBag.StatusMessage = "The file type must be a .mp3 type.";
                return View("AddSpeaker");
            }

            DateTimeOffset current_time = DateTimeOffset.UtcNow;
            long ms_time = current_time.ToUnixTimeMilliseconds();
            string new_file_name = speakerAbbreviation + "_" + ms_time.ToString() + ".mp3";

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
                return View("AddSpeaker");
            }

            if (LoggedInAppUser != null)
            {
                newSpeakerIn.SecretFileName = new_file_name;
                newSpeakerIn.DisplayFileName = speakerAbbreviation;
                newSpeakerIn.UploadedById = LoggedInAppUser.Id;
                newSpeakerIn.UploadedBy = LoggedInUserAbbreviation;
                var succeeded = _dbConnectionService.AddSpeaker(newSpeakerIn);

                if (succeeded)
                {
                    ViewBag.StatusMessage = "File uploaded successfully.";
                    return RedirectToAction("Index");
                }
                else
                {
                    ViewBag.StatusMessage = "Error uploading. Try again or contact development.";
                    return View("AddSpeaker");
                }
            }
            else
            {
                ViewBag.StatusMessage = "User not found.";
                return View("AddSpeaker");
            }
        }

        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> EditSpeakerDetails(string? Id)
        {
            if (!int.TryParse(Id, out int id))
            {
                return BadRequest("Invalid speaker Id format.");
            }
            SpeakerModel speaker = _dbConnectionService.GetSpeakerById(id);
            return View(speaker);
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        public async Task<IActionResult> UpdateSpeaker([Bind("SpeakerId, FileName, FirstName, LastName, Description, NumUpVotes, DateRecorded, UploadDate, UploadedBy, SpeakerStatus, FormFile")] SpeakerModel newSpeakerIn)
        {
            var LoggedInAppUser = await _userManager.GetUserAsync(User);
            // enforce speaker name formatting rules
            string speakerFirstNameInitial = newSpeakerIn.FirstName.Substring(0, 1).ToUpper();
            string speakerFirstNamePostFix = newSpeakerIn.FirstName.Substring(1).ToLower();
            string speakerFirstName = speakerFirstNameInitial + speakerFirstNamePostFix;
            newSpeakerIn.FirstName = speakerFirstName;
            string speakerLastNameInitial = newSpeakerIn.LastName.Substring(0, 1).ToUpper();
            string speakerLastNamePostFix = newSpeakerIn.LastName.Substring(1).ToLower();
            string speakerLastName = speakerLastNameInitial + speakerLastNamePostFix;
            newSpeakerIn.LastName = speakerLastName;

            string? LoggedInUserAbbreviation = LoggedInAppUser.FirstName + "_" + LoggedInAppUser.LastName.Substring(0, 1).ToUpper();

            DateTimeOffset current_time = DateTimeOffset.UtcNow;
            long ms_time = current_time.ToUnixTimeMilliseconds();
            string new_file_name = LoggedInUserAbbreviation + "_" + ms_time.ToString() + ".mp3";

            if (LoggedInAppUser != null)
            {
                var succeeded = _dbConnectionService.UpdateSpeakerDetails(newSpeakerIn);
                if (succeeded)
                {
                    ViewBag.StatusMessage = "Speaker updated successfully.";
                    return RedirectToAction("Index");
                }
                else
                {
                    ViewBag.StatusMessage = "Error updating. Try again or contact development.";
                    return View("UpdateSpeaker");
                }
            }
            else
            {
                ViewBag.StatusMessage = "User not found.";
                return View("UpdateSpeaker");
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