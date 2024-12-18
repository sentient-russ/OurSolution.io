using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace os.Models
{
    public class LogModel
    {
        [Key]
        [DisplayName("Event Id:")]
        public int? EventId { get; set; }

        [DisplayName("Event Date:")]
        public DateTime? EventDate { get; set; }

        [DisplayName("User Id:")]
        public string? UserId { get; set; }

        [DisplayName("Description:")]
        public string? Description { get; set; }

        [DisplayName("Changed From:")]
        public string? ChangedFrom { get; set; }

        [DisplayName("Changed To:")]
        public string? ChangedTo { get; set; }
    }
}
