using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class ResendConfirmationEmailViewModel
    {
        [Required]
        [Display(Name = "Email")]
        public string Email { get; set; }
    }
}