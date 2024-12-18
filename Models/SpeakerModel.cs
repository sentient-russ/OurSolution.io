using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace os.Models
{
    public class SpeakerModel
    {
        [Key]
        public int? SpeakerId { get; set; } = 0;
        public string? FileName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Description { get; set; }
        public int? NumUpVotes { get; set; }
        public DateTime? DateRecorded { get; set; }
        public DateTime? UploadDate { get; set; }
        public string? UploadedBy { get; set; }
        public string? SpeakerStatus { get; set; } = "Active";

        [NotMapped]
        [BindProperty]
        public IFormFile? FormFile { get; set; }
    }
}
