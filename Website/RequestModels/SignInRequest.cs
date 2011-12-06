using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class SignInRequest
    {
        [Required]
        [Display(Name = "Username")]
        [Hint("Enter your username.")]
        public string UserName { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Hint("Passwords must be at least 7 characters long.")]
        public string Password { get; set; }
    }
}