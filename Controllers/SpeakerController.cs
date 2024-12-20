using Microsoft.AspNetCore.Mvc;
using os.Models;
using os.Services;

namespace os.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MediaController : ControllerBase
    {
        private readonly string _uploadsFolderPath = Path.Combine("wwwroot", "uploads");
        private readonly DbConnectionService _connectionService;
        public MediaController(DbConnectionService dbConnectionService) { 
            _connectionService = dbConnectionService;
        }

        [HttpGet("files")]
        public IActionResult GetFiles()
        {
            try
            {
                List<SpeakerModel> speakers = _connectionService.GetSpeakersList();
                if (speakers == null || speakers.Count == 0)
                {
                    return NotFound("No speakers found."); // Return a 404 if no speakers are found
                }

                return Ok(speakers);
            }
            catch (Exception ex)
            {
                // Log the exception
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("stream/{fileName}")]
        public IActionResult StreamMp3(string fileName)
        {
            string secretFileName = "";
            List<SpeakerModel> speakers = _connectionService.GetSpeakersList();
            List<string> files = new List<string>();
            foreach(SpeakerModel speaker in speakers)
            {
                if (speaker.DisplayFileName == fileName)
                {
                    secretFileName = speaker.SecretFileName;
                    break;
                }
            }
            var filePath = Path.Combine(_uploadsFolderPath, secretFileName);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("File not found.");
            }

            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            return File(fileStream, "audio/mpeg", enableRangeProcessing: true);
        }
    }
}