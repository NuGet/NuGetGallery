using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class SignInRequest
    {
        [Required]
        [Display(Name = "Username or Email")]
        [Hint("Enter your username or email address.")]
        public string UserNameOrEmail { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Hint("Passwords must be at least 7 characters long.")]
        public string Password { get; set; }
    }
}