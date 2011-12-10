using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class ForgotPasswordViewModel
    {
        [Required]
        [Display(Name = "Email")]
        public string Email { get; set; }
    }
}