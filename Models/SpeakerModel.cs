/*SpeakerModel.cs*/
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace os.Models
{
    [ApiController]
    [Route("[Controller]")]
    [BindProperties(SupportsGet = true)]
    public class SpeakerModel
    {
        [Key]
        [DisplayName("Speaker Id:")]
        public int? SpeakerId { get; set; } = 0;

        [NotMapped] //Only used when uploading
        [DisplayName("File Name In:")]
        public string? FileName { get; set; }

        [Required]
        [DisplayName("First Name: (Speaker)")]
        [DataType(DataType.Text)]
        [StringLength(100, MinimumLength = 2)]
        public string? FirstName { get; set; }

        [DisplayName("Last Initial:")]
        [DataType(DataType.Text)]
        [StringLength(100, MinimumLength = 1)]
        public string? LastName { get; set; }

        [DisplayName("Description:")]
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
        public DateTime? UploadDate { get; set; } = DateTime.Now;

        [DisplayName("Uploaded By:")]
        public string? UploadedBy { get; set; }

        [DisplayName("Speaker Status:")]
        public string? SpeakerStatus { get; set; } = "Enabled";

        [DisplayName("File Name:")]
        public string? DisplayFileName { get; set; }

        [DisplayName("Secret Filename:")]
        public string? SecretFileName { get; set; }

        [DisplayName("Uploaded By Id:")]
        public string? UploadedById { get; set; }

        [DisplayName("Visibility:")]
        public string? Visibility { get; set; }

        [NotMapped]
        [Required]
        public IFormFile? FormFile { get; set; }

        [NotMapped]
        public List<IFormFile> CdaFiles { get; set; }

        [NotMapped]
        public string ConversionStatus { get; set; }

        [NotMapped]
        public string ConversionId { get; set; }
    }
}
