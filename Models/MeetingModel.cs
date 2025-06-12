using Org.BouncyCastle.Asn1.Mozilla;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace os.Models
{
    public class MeetingModel
    {
        [Key]
        public int? Id { get; set; }
        public string? MeetingName { get; set; }
        public string? LocationName { get; set; }
        public string? Weekday { get; set; }
        public string? StartTime { get; set; }
        public string? StartTimeAMPM { get; set; }
        public string? EndTime { get; set; }
        public string? EndTimeAMPM { get; set; }
        public string? StreetAddress { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Zip { get; set; }
        public string? Status { get; set; }
        public string? GoogleMapsLink { get; set; }
    }
}
