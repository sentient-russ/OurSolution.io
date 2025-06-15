using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using os.Areas.Identity.Data;
using os.Areas.Identity.Services;
using os.Models; // Replace with your actual namespace
using os.Services;
using System.Threading.Tasks;

namespace os.Controllers
{
    public class HomeController : Controller
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly DbConnectionService _dbConnectionService;
        private readonly IEmailSender _emailSender; 
        public HomeController(SignInManager<AppUser> signInManager,
            UserManager<AppUser> userManager,
            DbConnectionService dbConnectionService,
            IEmailSender emailSender) 
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _dbConnectionService = dbConnectionService;
            _emailSender = emailSender; 
        }

        [HttpGet]
        public IActionResult Index()
        {
            HomeBundle homeBundle = new HomeBundle();

            // Get announcements
            List<AnnouncementModel> announcementList = _dbConnectionService.GetAnnouncementList();
            homeBundle.AnnouncementList = announcementList;

            // Get meetings
            List<MeetingModel> meetingList = _dbConnectionService.GetMeetingList();
            homeBundle.MeetingList = meetingList.Where(m => m.Status == "Active").OrderBy(m => m.Weekday).ThenBy(m => m.StartTime).ToList();

            return View(homeBundle);
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

        public IActionResult SpeakerRemovalRequest()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SubmitSpeakerRemovalRequest(SpeakerRemovalRequestModel requestIn)
        {
             if (!ModelState.IsValid)
            {
                return View("SpeakerRemovalRequest", requestIn);
            }

            try
            {

                // Set default values for the request
                requestIn.RequestDate = DateTime.Now; // this prevents hackery
                requestIn.Status = "Pending"; // this prevents hackery            
                SpeakerModel fullSpeakerModel = _dbConnectionService.GetSpeakerById(Int32.Parse(requestIn.Speaker));
                requestIn.SpeakerFirstName = fullSpeakerModel.FirstName;
                requestIn.SpeakerLast = fullSpeakerModel.LastName;
                requestIn.Description = fullSpeakerModel.Description;
                requestIn.SpeakerId = fullSpeakerModel.SpeakerId.ToString();

                bool? succeeded = _dbConnectionService.StoreRemovalRequest(requestIn);

                List<RoleModel> adminsList = _dbConnectionService.GetUserRole("Administrator");                

                if (adminsList != null && adminsList.Count > 0)
                {
                    //Remove the default admin account because the email address is not actually valid.
                    List<RoleModel> finalAdminList = new List<RoleModel>();
                    foreach(var admin in adminsList)
                    {
                        if (!admin.email.Contains("admin@oursolution.io") && !admin.email.Contains("shareduser@hotmail.com"))
                        {
                            finalAdminList.Add(admin);
                        }
                    }

                    // Build the email subject and body with plain text
                    string emailSubject = $"Speaker Removal Request: {requestIn.SpeakerFirstName} {requestIn.SpeakerLast}";

                    string emailBody = $"SPEAKER REMOVAL REQUEST\r\n\r\n" +
                       $"A request has been submitted to remove a speaker from the system.\r\n\r\n" +
                       $"REQUEST DETAILS:\r\n" +
                       $"- Requester: {requestIn.FirstName} {requestIn.LastName}\r\n" +
                       $"- Contact Email: {requestIn.EmailAddress}\r\n" +
                       $"- Contact Phone: {requestIn.PhoneNumber}\r\n" +
                       $"- Relation to Speaker: {requestIn.RelationToSpeaker}\r\n" +
                       $"- Speaker: {requestIn.SpeakerFirstName} {requestIn.SpeakerLast}\r\n" +
                       $"- Speaker ID: {requestIn.SpeakerId ?? "Not provided"}\r\n" +
                       $"- Description: {requestIn.Description}\r\n" +
                       $"- Reason for Removal: {requestIn.RemovalReason}\r\n" +
                       $"- Request Date: {requestIn.RequestDate.ToString("yyyy-MM-dd HH:mm:ss")}\r\n\r\n" +
                       $"Please log in to the admin panel to review and process this request.";

                    // Send email to each administrator
                    foreach (var admin in finalAdminList)
                    {
                        if (!string.IsNullOrEmpty(admin.email))
                        {
                            await _emailSender.SendEmailAsync(
                                admin.email,
                                emailSubject,
                                emailBody
                            );
                        }
                    }

                    // Also notify the requester that their request was received
                    if (!string.IsNullOrEmpty(requestIn.EmailAddress))
                    {
                        string requesterEmailSubject = "Speaker Removal Request Received";
                        string requesterEmailBody = $"YOUR SPEAKER REMOVAL REQUEST HAS BEEN RECEIVED\r\n\r\n" +
                                                   $"Dear {requestIn.FirstName},\r\n\r\n" +
                                                   $"We have received your request to remove speaker {requestIn.SpeakerFirstName} {requestIn.SpeakerLast} from our system.\r\n\r\n" +
                                                   $"Your request will be reviewed by our administrators, and you will be notified of the decision.\r\n\r\n" +
                                                   $"Thank you for your patience.\r\n\r\n\r\n" +
                                                   $"OurSolution.io Team";

                        await _emailSender.SendEmailAsync(
                            requestIn.EmailAddress,
                            requesterEmailSubject,
                            requesterEmailBody
                        );
                    }

                    // Add a success message to TempData to display on the next page
                    TempData["StatusMessage"] = "Your speaker removal request has been submitted successfully. Administrators have been notified.";
                }
                else
                {
                    // No administrators found
                    TempData["StatusMessage"] = "Your request was processed, but we couldn't notify administrators. Please notify a litterature chair person at one of the meetings listed on our main page.";
                }
            }
            catch (Exception ex)
            {
                // Log the exception
                TempData["ErrorMessage"] = "An error occurred while processing your request. Please try again later.";
                // You might want to log this exception to a file or database
            }

            return RedirectToAction("Index");
        }


    }
}
