using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace NuGetGallery {
    public class SignInRequest {
        [Required]
        [DisplayName("Username")]
        [AdditionalMetadata("Hint", "Enter either your username or email address.")]
        public string UserNameOrEmail { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [AdditionalMetadata("Hint", "Passwords must be at least 7 characters long.")]
        public string Password { get; set; }
    }
}