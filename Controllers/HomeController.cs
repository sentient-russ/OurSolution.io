using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using os.Areas.Identity.Data;
using os.Areas.Identity.Services;
using os.Models; // Replace with your actual namespace
using os.Services;
using System.Diagnostics;
using System.Threading.Tasks;

namespace os.Controllers
{
    public class HomeController : Controller
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly DbConnectionService _dbConnectionService;
        private readonly IEmailSender _emailSender;
        private readonly ITranscriptionService _transcriptionService;
        //private readonly OllamaService _ollamaService;
        public HomeController(SignInManager<AppUser> signInManager,
            UserManager<AppUser> userManager,
            DbConnectionService dbConnectionService,
            IEmailSender emailSender,
            ITranscriptionService transcriptionService
            //OllamaService ollamaService
            ) 
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _dbConnectionService = dbConnectionService;
            _emailSender = emailSender;
            _transcriptionService = transcriptionService;
            //_ollamaService = ollamaService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            HomeBundle homeBundle = new HomeBundle();

            // Get announcements
            List<AnnouncementModel> announcementList = _dbConnectionService.GetAnnouncementList();
            homeBundle.AnnouncementList = announcementList;

            // Get meetings
            List<MeetingModel> meetingList = _dbConnectionService.GetMeetingList();
            homeBundle.MeetingList = meetingList.Where(m => m.Status == "Active").OrderBy(m => m.Weekday).ThenBy(m => m.StartTime).ToList();

            //// Testing transcription service - store result in ViewBag, not model
            //try
            //{
            //    var speaker = _dbConnectionService.GetSpeakerById(44);
            //    if (speaker != null)
            //    {
            //        // Add the force parameter to ensure it uses the latest implementation
            //        string transcription = await _transcriptionService.TranscribeSpeakerMp3(speaker, force: true);
            //        ViewBag.Transcription = transcription;
            //        ViewBag.TranscriptionInfo = $"Transcribed using {_transcriptionService.GetType().Name} with WhisperS2T";
            //    }
            //}
            //catch (Exception ex)
            //{
            //    ViewBag.Error = $"Error with transcription: {ex.Message}";

            //}
            // Testing transcription service - store result in ViewBag, not model
            //try
            //{
            //    var speaker = _dbConnectionService.GetSpeakerById(44);
            //    if (speaker != null)
            //    {
            //        // Add the force parameter to ensure it uses the latest implementation
            //        //string transcription = await _transcriptionService.TranscribeSpeakerMp3(speaker, force: true);
            //        // insert a way to input the transcription file name here.
            //        string transcription = await LoadTranscriptionFile("Boston_F_1749991986760_transcript.txt");
            //        ViewBag.Transcription = transcription;
            //        ViewBag.TranscriptionInfo = $"Transcribed using {_transcriptionService.GetType().Name} with WhisperS2T";

            //        Test OllamaService to extract speaker names from the transcription
            //        if (!string.IsNullOrEmpty(transcription) && _ollamaService != null)
            //        {
            //            try
            //            {
            //                var speakerNames = await _ollamaService.GetNames(transcription);
            //                ViewBag.SpeakerNames = speakerNames;
            //                ViewBag.OllamaInfo = $"Extracted {speakerNames.Count} speaker references using {_ollamaService.GetType().Name}";
            //            }
            //            catch (Exception ollamaEx)
            //            {
            //                ViewBag.OllamaError = $"Error extracting speaker names: {ollamaEx.Message}";
            //            }
            //        }

            using var httpClient = new HttpClient();
            var extractor = new SpeakerExtractor(httpClient);

            // Load the transcript file
            string fullTranscriptText = await LoadTranscriptionFile("Boston_F_1749991986760_transcript.txt");

            // Extract only the segments part (everything after "## Segments")
            string transcriptContext = "";
            int segmentsIndex = fullTranscriptText.IndexOf("## Segments");
            if (segmentsIndex >= 0)
            {
                transcriptContext = fullTranscriptText.Substring(segmentsIndex);

                // Approximate token count (roughly 4 characters per token for English text)
                int approximateTokenCount = transcriptContext.Length / 4;
                Debug.WriteLine($"Segments token count (approximate): {approximateTokenCount}");

                // If the text is too long, trim it
                const int maxTokens = 1000;
                if (approximateTokenCount > maxTokens)
                {
                    // Calculate approximately how many characters to keep
                    int maxChars = maxTokens * 4;

                    // Look for a segment break to trim at for cleaner cutting
                    int lastSegmentBreakPos = transcriptContext.LastIndexOf("[", maxChars);

                    // Choose the best position to trim at
                    int trimPosition = (lastSegmentBreakPos > 0) ? lastSegmentBreakPos : maxChars;

                    // Trim the text
                    transcriptContext = transcriptContext.Substring(0, trimPosition);
                    Debug.WriteLine($"Trimmed transcript to approximately {trimPosition / 4} tokens");
                }
            }
            else
            {
                // If "## Segments" is not found, use a shorter portion of the transcript
                transcriptContext = fullTranscriptText.Substring(0, Math.Min(fullTranscriptText.Length, 48000)); // 12K tokens
                Debug.WriteLine("Warning: No segments marker found in transcript. Using limited portion.");
            }

            try
            {
                var names = await extractor.ExtractSpeakersAsync(transcriptContext);

                foreach (var name in names)
                {
                    Debug.WriteLine($"Name: {name.Name}  Begins: {name.Start} Ends: {name.End}");
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            //var transcriptContext = @"## Segments
            //    [00:00:00.000 ? 00:00:04.080]  And the Boston Frank, I guess, is because I come in the way of a greater Boston.
            //      Word timestamps:
            //        [00:00:00.000]  And
            //        [00:00:00.480]  the
            //        [00:00:00.620]  Boston
            //        [00:00:00.940]  Frank,
            //        [00:00:01.520]  I
            //        [00:00:01.600]  guess,
            //        [00:00:01.900]  is
            //        [00:00:02.060]  because
            //        [00:00:02.320]  I
            //        [00:00:02.540]  come
            //        [00:00:02.740]  in
            //        [00:00:02.820]  the
            //        [00:00:02.960]  way
            //        [00:00:03.120]  of
            //        [00:00:03.300]  a
            //        [00:00:03.460]  greater
            //        [00:00:03.640]  Boston.
            //    [00:00:05.160 ? 00:00:09.320]  I've been in LA for many years. I've heard a lot of important things.
            //      Word timestamps:
            //        [00:00:05.160]  I've
            //        [00:00:05.440]  been
            //        [00:00:05.540]  in
            //        [00:00:05.660]  LA
            //        [00:00:05.840]  for
            //        [00:00:06.140]  many
            //        [00:00:06.440]  years.
            //        [00:00:07.660]  I've
            //        [00:00:07.840]  heard
            //        [00:00:07.940]  a
            //        [00:00:08.100]  lot
            //        [00:00:08.280]  of
            //        [00:00:08.380]  important
            //        [00:00:08.820]  things.";

            //try
            //{
            //    var names = await extractor.ExtractSpeakersAsync(transcriptContext);

            //    foreach (var name in names)
            //    {
            //        Debug.WriteLine($"Name: {name.Name}  Begins: {name.Start} Ends: {name.End}");
            //        Console.WriteLine();
            //    }
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine($"Error: {ex.Message}");
            //}





            // Always return the HomeBundle model that the view expects
            return View(homeBundle);
        }
        private async Task<string> LoadTranscriptionFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentNullException(nameof(fileName));

            string transcriptionsFolder = Path.Combine("wwwroot", "transcriptions");
            string filePath = Path.Combine(transcriptionsFolder, fileName);

            if (!System.IO.File.Exists(filePath))
                throw new FileNotFoundException($"Transcription file not found: {filePath}");

            return await System.IO.File.ReadAllTextAsync(filePath);
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
