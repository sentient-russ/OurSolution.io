using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
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
        [RequestSizeLimit(700 * 1024 * 1024)] // 700MB
        public async Task<IActionResult> UploadSpeaker([Bind("SpeakerId, FileName, FirstName, LastName, Description, NumUpVotes, DateRecorded, UploadDate, UploadedBy, SpeakerStatus, Visibility, FormFile, CdaFiles")] SpeakerModel newSpeakerIn)
        {
            //CDA conversion requires the FFmpeg command line utility to be installed. It is not a NuGet package.
            //for Windows choco install ffmpeg
            //for Debian Linux apt-get install ffmpeg
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

            // Check if we have CDA files to process
            if (newSpeakerIn.CdaFiles != null && newSpeakerIn.CdaFiles.Count > 0)
            {
                // Inject the CDA conversion service
                var cdaService = HttpContext.RequestServices.GetRequiredService<CdaConversionService>();
                
                // Queue the conversion job
                var jobId = cdaService.QueueConversion(newSpeakerIn.CdaFiles, speakerAbbreviation);
                
                // Store the speaker info and job ID in TempData to be used by the modal
                TempData["ConversionJobId"] = jobId;
                TempData["SpeakerFirstName"] = newSpeakerIn.FirstName;
                TempData["SpeakerLastName"] = newSpeakerIn.LastName;
                TempData["SpeakerDescription"] = newSpeakerIn.Description;
                TempData["SpeakerDateRecorded"] = newSpeakerIn.DateRecorded?.ToString("yyyy-MM-dd");
                TempData["SpeakerStatus"] = newSpeakerIn.SpeakerStatus;
                TempData["SpeakerVisibility"] = newSpeakerIn.Visibility;
                
                // Return to AddSpeaker view with a flag to show the conversion modal
                ViewBag.ShowConversionModal = true;
                ViewBag.ConversionJobId = jobId;
                return View("AddSpeaker");
            }
            
            // Handle direct MP3 upload (existing code)
            if (newSpeakerIn.FormFile == null || newSpeakerIn.FormFile.Length == 0)
            {
                ViewBag.StatusMessage = "No file selected.";
                return View("AddSpeaker");
            }

            const long max_size = 700 * 1024 * 1024;
            if (newSpeakerIn.FormFile.Length > max_size)
            {
                ViewBag.StatusMessage = $"The file size must be less than 200MB. The current file is {newSpeakerIn.FormFile.Length.ToString()}";
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
        public IActionResult ConversionStatus(string jobId)
        {
            if (string.IsNullOrEmpty(jobId))
            {
                return NotFound();
            }
            
            var cdaService = HttpContext.RequestServices.GetRequiredService<CdaConversionService>();
            var job = cdaService.GetJobStatus(jobId);
            
            if (job == null)
            {
                return NotFound();
            }
            
            // If the job is complete, save the speaker to database
            if (job.Status == "Completed" && !string.IsNullOrEmpty(job.OutputFileName))
            {
                // Create a new speaker model using TempData values
                var speaker = new SpeakerModel
                {
                    FirstName = TempData["SpeakerFirstName"]?.ToString(),
                    LastName = TempData["SpeakerLastName"]?.ToString(),
                    Description = TempData["SpeakerDescription"]?.ToString(),
                    DateRecorded = DateTime.TryParse(TempData["SpeakerDateRecorded"]?.ToString(), out var date) ? date : DateTime.Now,
                    SpeakerStatus = TempData["SpeakerStatus"]?.ToString() ?? "Active",
                    Visibility = TempData["SpeakerVisibility"]?.ToString() ?? "Private",
                    UploadDate = DateTime.UtcNow,
                    SecretFileName = job.OutputFileName,
                    DisplayFileName = job.SpeakerAbbreviation,
                    UploadedById = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    UploadedBy = $"{User.FindFirstValue(ClaimTypes.GivenName)}_{User.FindFirstValue(ClaimTypes.Surname)?.Substring(0, 1).ToUpper()}"
                };
                
                var succeeded = _dbConnectionService.AddSpeaker(speaker);
                
                if (succeeded)
                {
                    ViewBag.StatusMessage = "Conversion completed and speaker saved successfully.";
                    ViewBag.Complete = true;
                }
                else
                {
                    ViewBag.StatusMessage = "Conversion completed but error saving speaker.";
                    ViewBag.Error = true;
                }
            }
            
            return View(job);
        }

        [Authorize(Roles = "Administrator")]
        [HttpGet]
        [Route("api/conversion-status/{jobId}")]
        public IActionResult GetConversionStatus(string jobId)
        {
            if (string.IsNullOrEmpty(jobId))
            {
                return NotFound();
            }
            
            var cdaService = HttpContext.RequestServices.GetRequiredService<CdaConversionService>();
            var job = cdaService.GetJobStatus(jobId);
            
            if (job == null)
            {
                return NotFound();
            }
            
            // If the job is complete, save the speaker to database
            if (job.Status == "Completed" && !string.IsNullOrEmpty(job.OutputFileName) && TempData.Peek("SpeakerFirstName") != null)
            {
                // Create a new speaker model using TempData values
                var speaker = new SpeakerModel
                {
                    FirstName = TempData["SpeakerFirstName"]?.ToString(),
                    LastName = TempData["SpeakerLastName"]?.ToString(),
                    Description = TempData["SpeakerDescription"]?.ToString(),
                    DateRecorded = DateTime.TryParse(TempData["SpeakerDateRecorded"]?.ToString(), out var date) ? date : DateTime.Now,
                    SpeakerStatus = TempData["SpeakerStatus"]?.ToString() ?? "Active",
                    Visibility = TempData["SpeakerVisibility"]?.ToString() ?? "Private",
                    UploadDate = DateTime.UtcNow,
                    SecretFileName = job.OutputFileName,
                    DisplayFileName = job.SpeakerAbbreviation,
                    UploadedById = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    UploadedBy = $"{User.FindFirstValue(ClaimTypes.GivenName)}_{User.FindFirstValue(ClaimTypes.Surname)?.Substring(0, 1).ToUpper()}"
                };
                
                var succeeded = _dbConnectionService.AddSpeaker(speaker);
                
                if (succeeded)
                {
                    job.Complete = true;
                    job.SuccessMessage = "Conversion completed and speaker saved successfully.";
                }
                else
                {
                    job.Error = true;
                    job.ErrorMessage = "Conversion completed but error saving speaker.";
                }
                
                // Clear the TempData after successful processing
                TempData.Remove("SpeakerFirstName");
                TempData.Remove("SpeakerLastName");
                TempData.Remove("SpeakerDescription");
                TempData.Remove("SpeakerDateRecorded");
                TempData.Remove("SpeakerStatus");
                TempData.Remove("SpeakerVisibility");
                TempData.Remove("ConversionJobId");
            }
            
            return Json(job);
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
        public async Task<IActionResult> UpdateSpeaker([Bind("SpeakerId, FileName, FirstName, LastName, Description, NumUpVotes, DateRecorded, UploadDate, UploadedBy, SpeakerStatus, Visibility, FormFile")] SpeakerModel newSpeakerIn)
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

        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> ViewAnnouncements()
        {
            List<AnnouncementModel> announcementList = new List<AnnouncementModel>();
            announcementList = _dbConnectionService.GetAnnouncementList();
            announcementList.Reverse();
            return View(announcementList);
        }


        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public IActionResult AddAnnouncement()
        {
            // Show empty form for new announcement
            return View(new AnnouncementModel());
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddAnnouncement(AnnouncementModel model)
        {
            if (ModelState.IsValid)
            {
                model.AnnouncementDate = DateTime.UtcNow;
                model.Status = string.IsNullOrEmpty(model.Status) ? "Enabled" : model.Status;
                var result = _dbConnectionService.AddAnnouncement(model);
                if (result != null)
                {
                    return RedirectToAction(nameof(ViewAnnouncements));
                }
                ModelState.AddModelError("", "Failed to add announcement. Please try again.");
            }
            return View(model);
        }


        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public IActionResult RemoveAnnouncement(int id)
        {
            // Optionally, fetch the announcement for confirmation
            var announcement = _dbConnectionService.GetAnnouncementList().FirstOrDefault(a => a.Id == id);
            if (announcement == null)
            {
                return NotFound();
            }
            return RemoveAnnouncementConfirmed(id);
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost, ActionName("RemoveAnnouncement")]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveAnnouncementConfirmed(int id)
        {
            var announcements = _dbConnectionService.GetAnnouncementList();
            var announcement = announcements.FirstOrDefault(a => a.Id == id);
            if (announcement == null)
            {
                return NotFound();
            }
            var result = _dbConnectionService.DeleteAnnouncement(id);
            if (result != null)
            {
                return RedirectToAction(nameof(ViewAnnouncements));
            }
            return View(announcement);
        }

        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public IActionResult UpdateAnnouncement(int id)
        {
            var announcement = _dbConnectionService.GetAnnouncementList().FirstOrDefault(a => a.Id == id);
            if (announcement == null)
            {
                return NotFound();
            }
            return View(announcement);
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateAnnouncement(AnnouncementModel model)
        {
            if (ModelState.IsValid)
            {
                var result = _dbConnectionService.UpdateAnnouncement(model);
                if (result != null)
                {
                    return RedirectToAction(nameof(ViewAnnouncements));
                }
                ModelState.AddModelError("", "Failed to update announcement. Please try again.");
            }
            return View(model);
        }
        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> ViewMeetings()
        {
            List<MeetingModel> meetingList = new List<MeetingModel>();
            meetingList = _dbConnectionService.GetMeetingList();
            meetingList = meetingList.OrderBy(m => m.Weekday).ThenBy(m => m.StartTime).ToList();
            return View(meetingList);
        }

        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public IActionResult AddMeeting()
        {
            // Show empty form for new meeting
            return View(new MeetingModel { Status = "Active" });
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddMeeting(MeetingModel model)
        {
            if (ModelState.IsValid)
            {
                model.Status = string.IsNullOrEmpty(model.Status) ? "Active" : model.Status;
                var result = _dbConnectionService.AddMeeting(model);
                if (result != null)
                {
                    return RedirectToAction(nameof(ViewMeetings));
                }
                ModelState.AddModelError("", "Failed to add meeting. Please try again.");
            }
            return View(model);
        }

        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public IActionResult RemoveMeeting(int id)
        {
            // Fetch the meeting for confirmation
            var meeting = _dbConnectionService.GetMeetingList().FirstOrDefault(m => m.Id == id);
            if (meeting == null)
            {
                return NotFound();
            }
            return RemoveMeetingConfirmed(id);
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost, ActionName("RemoveMeeting")]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveMeetingConfirmed(int id)
        {
            var meetings = _dbConnectionService.GetMeetingList();
            var meeting = meetings.FirstOrDefault(m => m.Id == id);
            if (meeting == null)
            {
                return NotFound();
            }
            var result = _dbConnectionService.DeleteMeeting(id);
            if (result != null)
            {
                return RedirectToAction(nameof(ViewMeetings));
            }
            return View(meeting);
        }

        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public IActionResult UpdateMeeting(int id)
        {
            var meeting = _dbConnectionService.GetMeetingList().FirstOrDefault(m => m.Id == id);
            if (meeting == null)
            {
                return NotFound();
            }
            return View(meeting);
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateMeeting(MeetingModel model)
        {
            if (ModelState.IsValid)
            {
                var result = _dbConnectionService.UpdateMeeting(model);
                if (result != null)
                {
                    return RedirectToAction(nameof(ViewMeetings));
                }
                ModelState.AddModelError("", "Failed to update meeting. Please try again.");
            }
            return View(model);
        }
    }

}