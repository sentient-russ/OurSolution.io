using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace os.Models
{
    public class SpeakerModel
    {
        [Key]
        [DisplayName("Speaker Id:")]
        public int? SpeakerId { get; set; } = 0;

        [Required]
        [DisplayName("Speaker First Name:")]
        [DataType(DataType.Text)]
        [StringLength(100, MinimumLength = 2)]
        public string? FirstName { get; set; }

        [DisplayName("Speaker Last Name / Initial:")]
        [DataType(DataType.Text)]
        [StringLength(100, MinimumLength = 1)]
        public string? LastName { get; set; }

        [DisplayName("Recording Description:")]
        [DataType(DataType.Text)]
        [StringLength(500, MinimumLength = 1)]
        public string? Description { get; set; }

        [DisplayName("Up-Votes:")]
        public int? NumUpVotes { get; set; }

        [DisplayName("Date Recorded:")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [DataType(DataType.Date)]
        public DateTime? DateRecorded { get; set; }

        [DisplayName("Date Uploaded:")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [DataType(DataType.Date)]
        public DateTime? UploadDate { get; set; }

        [DisplayName("Uploaded By:")]
        public string? UploadedBy { get; set; }

        [DisplayName("Speaker Status:")]
        public string? SpeakerStatus { get; set; } = "Active";

        [DisplayName("File Name:")]
        public string? FileName { get; set; }

        [NotMapped]
        [Required]
        [BindProperty]
        public IFormFile? FormFile { get; set; }
    }
}
