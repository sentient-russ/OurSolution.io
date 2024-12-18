using Microsoft.AspNetCore.Mvc;

namespace os.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MediaController : ControllerBase
    {
        private readonly string _uploadsFolderPath = Path.Combine("wwwroot", "uploads");

        // Endpoint to get the list of MP3 files
        [HttpGet("files")]
        public IActionResult GetFiles()
        {
            if (!Directory.Exists(_uploadsFolderPath))
            {
                return NotFound("Uploads folder not found.");
            }

            var files = Directory.GetFiles(_uploadsFolderPath, "*.mp3")
                                 .Select(Path.GetFileName)
                                 .ToList();
            return Ok(files);
        }

        // Endpoint to stream an MP3 file
        [HttpGet("stream/{fileName}")]
        public IActionResult StreamMp3(string fileName)
        {
            var filePath = Path.Combine(_uploadsFolderPath, fileName);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("File not found.");
            }

            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            return File(fileStream, "audio/mpeg", enableRangeProcessing: true);
        }
    }
}