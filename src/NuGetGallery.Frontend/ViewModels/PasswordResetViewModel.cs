using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class PasswordResetViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        [StringLength(64, MinimumLength = 7)]
        [Hint("Passwords must be at least 7 characters long.")]
        public string NewPassword { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }
}