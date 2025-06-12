using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices.JavaScript;

namespace os.Models
{
    public class AnnouncementModel
    {
        [Key]
        public int? Id { get; set; }
        public string? AnnouncementTxt { get; set; }
        public DateTime? AnnouncementDate { get; set; }
        public string? Status { get; set; } // enabled, disabled
    }
}
