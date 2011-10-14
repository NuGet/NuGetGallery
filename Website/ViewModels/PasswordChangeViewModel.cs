using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class PasswordChangeViewModel : PasswordResetViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string OldPassword { get; set; }
    }
}