using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace os.Models
{
    [ApiController]
    [Route("[Controller]")]
    [BindProperties(SupportsGet = true)]
    public class AppUserModel
    {
        static DateTime today = System.DateTime.Now;

        [Key]
        public int? Id { get; set; }

        [Required]
        [DataType(DataType.Text)]
        [StringLength(100, MinimumLength = 1)]
        [DisplayName("First Name:")]
        public string? FirstName { get; set; } = "";

        [Required]
        [DataType(DataType.Text)]
        [StringLength(100, MinimumLength = 1)]
        [DisplayName("Last Name:")]
        public string? LastName { get; set; } = "";

        [DataType(DataType.Text)]
        [StringLength(100, MinimumLength = 1)]
        [DisplayName("Phone Number:")]
        public string? PhoneNumber { get; set; } = "";

        [Required]
        [DataType(DataType.Text)]
        [StringLength(100, MinimumLength = 1)]
        [DisplayName("Email:")]
        public string? Email { get; set; } = "";

        [DisplayFormat(DataFormatString = "{:dd MMM yyyy}", ApplyFormatInEditMode = true)]
        [DataType(DataType.Date)]
        [DisplayName("Belly Button Birthday:")]
        public DateTime? BellyButtonBirthday { get; set; } = today;

        [DisplayFormat(DataFormatString = "{:dd MMM yyyy}", ApplyFormatInEditMode = true)]
        [DataType(DataType.Date)]
        [DisplayName("AA Birthday:")]
        public DateTime? AABirthday { get; set; } = today;

        [DataType(DataType.Text)]
        [StringLength(100, MinimumLength = 1)]
        [DisplayName("Street Address:")]
        public string? Address { get; set; } = "";

        [DataType(DataType.Text)]
        [StringLength(100, MinimumLength = 1)]
        [DisplayName("City:")]
        public string? City { get; set; } = "";

        [DataType(DataType.Text)]
        [StringLength(2, MinimumLength = 2)]
        [DisplayName("State:")]
        public string? State { get; set; } = "";

        [DataType(DataType.Text)]
        [StringLength(10, MinimumLength = 1)]
        [DisplayName("Zip:")]
        public string? Zip { get; set; } = "";

        [Required]
        [DataType(DataType.Text)]
        [StringLength(100, MinimumLength = 1)]
        [DisplayName("User Role:")]
        public string? UserRole { get; set; } = "Member";

        [Required]
        [DataType(DataType.Text)]
        [StringLength(10, MinimumLength = 1)]
        [DisplayName("Active Status:")]
        public string? ActiveStatus { get; set; } = "Active";

        [DataType(DataType.Text)]
        [StringLength(500, MinimumLength = 1)]
        public string? ProfileImage { get; set; }

        [NotMapped]
        public IEnumerable<SelectListItem>? RoleList { get; set; }
    }
}
