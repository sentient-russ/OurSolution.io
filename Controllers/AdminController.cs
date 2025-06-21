using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using os.Areas.Identity.Data;
using os.Migrations;
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
        private readonly IEmailSender _emailSender;
        private readonly AudioReplacementService _audioReplacementService;
        public AdminController(UserManager<AppUser> userManager, 
            DbConnectionService dbConnectionService, 
            ApplicationDbContext DbContext, 
            RoleManager<IdentityRole> roleManager,
            IEmailSender emailSender,
            AudioReplacementService audioReplacementService)
        {
            _userManager = userManager;
            _dbConnectionService = dbConnectionService;
            _DbContext = DbContext;
            _roleManager = roleManager;
            _emailSender = emailSender;
            _audioReplacementService = audioReplacementService;
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
        public async Task<IActionResult> UploadSpeaker([Bind("SpeakerId, FileName, FirstName, LastName, Description, NumUpVotes, DateRecorded, UploadDate, UploadedBy, SpeakerStatus, Visibility, ReplacedNames, FormFile")] SpeakerModel newSpeakerIn, List<NameItem> Names = null, int? existingSpeakerId = null)
        {
            var LoggedInAppUser = await _userManager.GetUserAsync(User);

            // This code block is skipped on the first call and run on a subsequent call once the user has been prompted to make name removal selections
            if (Names != null && existingSpeakerId.HasValue)
            {
                try
                {
                    // Get the existing speaker data
                    SpeakerModel existingSpeaker = _dbConnectionService.GetSpeakerById(existingSpeakerId.Value);
                    if (existingSpeaker == null)
                    {
                        ViewBag.StatusMessage = "Speaker not found.";
                        return View("AddSpeaker");
                    }

                    // Convert the selected names to CutSpeakerModel
                    var selectedNames = Names
                        .Where(n => n.Selected)
                        .Select(n => new CutSpeakerModel
                        {
                            Name = n.Name,
                            Start = n.Start,
                            End = n.End
                        })
                        .ToList();


                    // Process the audio file
                    string? anonymizedFileName = await _audioReplacementService.ReplaceAudioAsync(existingSpeakerId.Value, selectedNames);

                    // Save the anonymized file name to the database
                    if (!string.IsNullOrEmpty(anonymizedFileName))
                    {
                        SpeakerModel updatedSpeaker = _dbConnectionService.GetSpeakerById(existingSpeakerId.Value);
                        // Get just the filename part
                        string fileName = Path.GetFileName(anonymizedFileName);
                        updatedSpeaker.SecretFileName = fileName;
                        _dbConnectionService.UpdateSpeakerDetails(updatedSpeaker);
                        
                        ViewBag.StatusMessage = "File processed successfully. Names have been replaced.";
                        return RedirectToAction("Index");
                    }
                    else
                    {
                        ViewBag.StatusMessage = "There was an error processing the audio.";
                        return View("AddSpeaker");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during audio replacement: {ex.Message}");
                    ViewBag.StatusMessage = "Error replacing names: " + ex.Message;
                    return View("AddSpeaker");
                }
            }

            // Normal file upload path
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

            // Handle MP3 upload
            if (newSpeakerIn.FormFile == null || newSpeakerIn.FormFile.Length == 0)
            {
                ViewBag.StatusMessage = "No file selected.";
                return View("AddSpeaker");
            }

            const long max_size = 700 * 1024 * 1024;
            if (newSpeakerIn.FormFile.Length > max_size)
            {
                ViewBag.StatusMessage = $"The file size must be less than 700MB. The current file is {newSpeakerIn.FormFile.Length} bytes.";
                return View("AddSpeaker");
            }

            var fileExtension = Path.GetExtension(newSpeakerIn.FormFile.FileName).ToLowerInvariant();
            if (fileExtension != ".mp3")
            {
                ViewBag.StatusMessage = "The file type must be a .mp3 type.";
                return View("AddSpeaker");
            }

            // Validate MP3 format by checking file signature
            using (var stream = newSpeakerIn.FormFile.OpenReadStream())
            {
                byte[] header = new byte[10]; // Read enough bytes to check for MP3 headers
                if (await stream.ReadAsync(header, 0, header.Length) < 3)
                {
                    ViewBag.StatusMessage = "The file is too small to be a valid MP3.";
                    return View("AddSpeaker");
                }

                bool isValidMp3 = false;

                // Check for ID3v2 tag (ID3)
                if (header[0] == 0x49 && header[1] == 0x44 && header[2] == 0x33)
                {
                    isValidMp3 = true;
                }
                // Check for MPEG frame sync (common MP3 frame header starts with 11 bits set)
                // Most MP3 files will have a sync word that starts with 0xFF followed by 0xF...
                else if (header[0] == 0xFF && (header[1] & 0xE0) == 0xE0)
                {
                    isValidMp3 = true;
                }

                if (!isValidMp3)
                {
                    ViewBag.StatusMessage = "The file does not appear to be a valid MP3 file.";
                    return View("AddSpeaker");
                }
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
                var newSpeakerId = _dbConnectionService.AddSpeaker(newSpeakerIn);

                if (newSpeakerId == 0)
                {
                    ViewBag.StatusMessage = "Error uploading. Try again or contact development.";
                    return View("AddSpeaker");
                }

                // Check if we're in the name selection phase
                if (newSpeakerIn.ReplacedNames)
                {
                    try
                    {
                        // Get names for selection and pass to view
                        var namesResult = await _audioReplacementService.GetNamesForSelectionAsync(newSpeakerId);
                        
                        if (namesResult.Names.Count > 0)
                        {
                            var nameSelectionModel = new NameSelectionModel
                            {
                                SpeakerId = newSpeakerId,
                                SpeakerName = $"{newSpeakerIn.FirstName} {newSpeakerIn.LastName}",
                                Names = namesResult.Names.Select(n => new NameItem
                                {
                                    Name = n.Name,
                                    Start = n.Start,
                                    End = n.End,
                                    Selected = true
                                }).ToList()
                            };
                            
                            ViewBag.NameSelectionModel = nameSelectionModel;
                            ViewBag.StatusMessage = "File uploaded successfully. Please select which names you want to remove.";
                            return View("AddSpeaker", new SpeakerModel { SpeakerId = newSpeakerId });
                        }
                        else
                        {
                            ViewBag.StatusMessage = "File uploaded successfully, but no names were detected to remove.";
                            return RedirectToAction("Index");
                        }
                    }
                    catch (Exception ex)
                    {
                        ViewBag.StatusMessage = "Error analyzing audio for names: " + ex.Message;
                        return View("AddSpeaker");
                    }
                }
                else
                {
                    ViewBag.StatusMessage = "File uploaded successfully.";
                    return RedirectToAction("Index");
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
        [RequestSizeLimit(700 * 1024 * 1024)] // 700MB
        public async Task<IActionResult> UpdateSpeaker([Bind("SpeakerId, FileName, FirstName, LastName, Description, NumUpVotes, DateRecorded, UploadDate, UploadedBy, SpeakerStatus, Visibility, FormFile")] SpeakerModel newSpeakerIn)
        {
            var LoggedInAppUser = await _userManager.GetUserAsync(User);
            if (LoggedInAppUser == null)
            {
                ViewBag.StatusMessage = "User not found.";
                return View("EditSpeakerDetails", newSpeakerIn);
            }

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
            string? speakerAbbreviation = newSpeakerIn.FirstName + "_" + newSpeakerIn.LastName.Substring(0, 1).ToUpper();

            // Handle file upload if provided (optional)
            if (newSpeakerIn.FormFile != null && newSpeakerIn.FormFile.Length > 0)
            {
                // Check file size
                const long max_size = 700 * 1024 * 1024; // 700MB
                if (newSpeakerIn.FormFile.Length > max_size)
                {
                    ViewBag.StatusMessage = $"The file size must be less than 700MB. The current file is {newSpeakerIn.FormFile.Length} bytes.";
                    return View("EditSpeakerDetails", newSpeakerIn);
                }

                // Check file extension first
                var fileExtension = Path.GetExtension(newSpeakerIn.FormFile.FileName).ToLowerInvariant();
                if (fileExtension != ".mp3")
                {
                    ViewBag.StatusMessage = "The file type must be a .mp3 type.";
                    return View("EditSpeakerDetails", newSpeakerIn);
                }

                // Validate MP3 format by checking file signature
                using (var stream = newSpeakerIn.FormFile.OpenReadStream())
                {
                    byte[] header = new byte[10]; // Read enough bytes to check for MP3 headers
                    if (await stream.ReadAsync(header, 0, header.Length) < 3)
                    {
                        ViewBag.StatusMessage = "The file is too small to be a valid MP3.";
                        return View("EditSpeakerDetails", newSpeakerIn);
                    }

                    bool isValidMp3 = false;

                    // Check for ID3v2 tag (ID3)
                    if (header[0] == 0x49 && header[1] == 0x44 && header[2] == 0x33)
                    {
                        isValidMp3 = true;
                    }
                    // Check for MPEG frame sync (common MP3 frame header starts with 11 bits set)
                    // Most MP3 files will have a sync word that starts with 0xFF followed by 0xF...
                    else if (header[0] == 0xFF && (header[1] & 0xE0) == 0xE0)
                    {
                        isValidMp3 = true;
                    }

                    if (!isValidMp3)
                    {
                        ViewBag.StatusMessage = "The file does not appear to be a valid MP3 file.";
                        return View("EditSpeakerDetails", newSpeakerIn);
                    }
                }

                // Process and save the file
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
                    
                    // Update the file name in the model
                    newSpeakerIn.SecretFileName = new_file_name;
                    newSpeakerIn.DisplayFileName = speakerAbbreviation;
                }
                catch (Exception ex)
                {
                    ViewBag.StatusMessage = "Error uploading file: " + ex.Message;
                    return View("EditSpeakerDetails", newSpeakerIn);
                }
            }
            else
            {
                // No file provided - only updating speaker details
                // Make sure we're not overwriting existing file information
                if (newSpeakerIn.SpeakerId.HasValue)
                {
                    // Get existing speaker to preserve file information
                    var existingSpeaker = _dbConnectionService.GetSpeakerById(newSpeakerIn.SpeakerId.Value);
                    if (existingSpeaker != null)
                    {
                        // Preserve the existing file information
                        newSpeakerIn.SecretFileName = existingSpeaker.SecretFileName;
                        newSpeakerIn.DisplayFileName = existingSpeaker.DisplayFileName;
                    }
                }
            }

            // Update speaker details in database
            var succeeded = _dbConnectionService.UpdateSpeakerDetails(newSpeakerIn);
            if (succeeded)
            {
                ViewBag.StatusMessage = "Speaker updated successfully.";
                return RedirectToAction("Index");
            }
            else
            {
                ViewBag.StatusMessage = "Error updating. Try again or contact development.";
                return View("EditSpeakerDetails", newSpeakerIn);
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

        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public IActionResult DeleteRemovalRequest(int id)
        {
            SpeakerRemovalRequestModel request = _dbConnectionService.GetRemovalRequestByTableId(id);
            if (request?.Id == null)
            {
                TempData["ErrorMessage"] = "Request not found.";
                return RedirectToAction("ManageRemovalRequests");
            }

            bool success = _dbConnectionService.DeleteRemovalRequest(id);
            if (success)
            {
                TempData["StatusMessage"] = "Request deleted successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to delete request.";
            }

            return RedirectToAction("ViewRemovalRequests");
        }

        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public IActionResult ViewRemovalRequests()
        {
            List<SpeakerRemovalRequestModel> requests = _dbConnectionService.GetRemovalRequests();
            return View(requests);
        }

        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public IActionResult ViewRemovalRequest(int id)
        {
            SpeakerRemovalRequestModel request = _dbConnectionService.GetRemovalRequestByTableId(id);
            if (request?.Id == null)
            {
                TempData["ErrorMessage"] = "Request not found.";
                return RedirectToAction("ManageRemovalRequests");
            }
            return View(request);
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        public async Task<IActionResult> ProcessRemovalRequest(SpeakerRemovalRequestModel updatedRequest)
        {
            SpeakerRemovalRequestModel orriginalRequest = _dbConnectionService.GetRemovalRequestBySpeakerId(updatedRequest.SpeakerId);
            if (orriginalRequest?.Id == null)
            {
                TempData["ErrorMessage"] = "Request not found.";
                return RedirectToAction("ManageRemovalRequests");
            }


            // Only process if the status is changing
            if (updatedRequest.Status != orriginalRequest.Status)
            {
                // Update the status in the database
                bool success = _dbConnectionService.UpdateRemovalRequestStatus(orriginalRequest.Id, updatedRequest.Status);

                if (success)
                {
                    // If the request is approved and there's a speaker ID, update the speaker status
                    if (updatedRequest.Status == "Approved" && !string.IsNullOrEmpty(updatedRequest.SpeakerId) &&
                        int.TryParse(updatedRequest.SpeakerId, out int speakerId))
                    {
                        SpeakerModel speaker = _dbConnectionService.GetSpeakerById(speakerId);
                        if (speaker?.SpeakerId != null)
                        {
                            speaker.SpeakerStatus = "Disabled";
                            _dbConnectionService.UpdateSpeakerDetails(speaker);
                        }
                    }

                    // Send email notification to the requester
                    if (!string.IsNullOrEmpty(updatedRequest.EmailAddress))
                    {
                        string emailSubject = $"Speaker Removal Request {updatedRequest.Status}";
                        string emailBody = $"SPEAKER REMOVAL REQUEST {updatedRequest.Status.ToUpper()}\r\n\r\n" +
                          $"Dear {updatedRequest.FirstName},\r\n\r\n" +
                          $"Your request to remove speaker {updatedRequest.SpeakerFirstName} {updatedRequest.SpeakerLast} has been {updatedRequest.Status.ToLower()}.\r\n\r\n" +
                          $"{(updatedRequest.Status == "Approved" ? "The speaker has been removed from our public listings." : "The administrator has reviewed your request and determined that the speaker should remain in our listings. Please feel free to resubmit your request with more information if you disagree with this decision.")}\r\n\r\n" +
                          $"Thank you for your patience.\r\n\r\n" +
                          $"OurSolution.io Team";

                        await _emailSender.SendEmailAsync(
                            updatedRequest.EmailAddress,
                            emailSubject,
                            emailBody
                        );
                    }

                    TempData["StatusMessage"] = $"Request has been {updatedRequest.Status.ToLower()} successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to update request status.";
                }
            } else
            {
                TempData["ErrorMessage"] = "Failed to update request status.";
            }

            return RedirectToAction("ViewRemovalRequests");
        }


    }

}