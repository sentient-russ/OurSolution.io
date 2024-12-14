using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Http;
//using static System.Runtime.InteropServices.JavaScript.JSType;

namespace os.Areas.Identity.Data
{
    [ApiController]
    [Route("[Controller]")]
    [BindProperties(SupportsGet = true)]
    public class AppUser : IdentityUser
    {

        static DateTime today = System.DateTime.Now;

        public string? FirstName { get; set; } = "";
        public string? LastName { get; set; } = "";
        public override string? PhoneNumber { get; set; } = "";
        public DateTime? BellyButtonBirthday { get; set; } = today;
        public DateTime? AABirthday { get; set; } = today;
        public string? Address { get; set; } = "";
        public string? City { get; set; } = "";
        public string? State { get; set; } = "";
        public string? Zip { get; set; } = "";
        public string? UserRole { get; set; } = "";
        public bool? ActiveStatus { get; set; } = true;

        [NotMapped]
        public IEnumerable<SelectListItem>? RoleList { get; set; }

        [DataType(DataType.Text)]
        [StringLength(500, MinimumLength = 1)]
        public string? ProfileImage { get; set; }

    }

}