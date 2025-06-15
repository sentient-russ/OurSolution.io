using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace os.Models
{
    public class SpeakerRemovalRequestModel
    {
        [Key]
        public int? Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? SpeakerFirstName { get; set; }
        public string? SpeakerLast { get; set; }
        public string? Description { get; set; }
        public string? SpeakerId { get; set; }
        public string? RelationToSpeaker { get; set; }
        public string? EmailAddress { get; set; }
        public string? PhoneNumber { get; set; }
        public string? RemovalReason { get; set; }
        public DateTime RequestDate { get; set; } = DateTime.UtcNow;
        public string? Status { get; set; } = "Pending"; // approved, rejected, pending

        [NotMapped]
        public string? Speaker { get; set; }

    }
}
