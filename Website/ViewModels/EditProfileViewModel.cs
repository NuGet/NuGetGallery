using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class EditProfileViewModel
    {
        [Required]
        [StringLength(255)]
        [Display(Name = "Email")]
        [DataType(DataType.EmailAddress)]
        [RegularExpression(RegisterRequest.EmailValidationRegex,
            ErrorMessage = "This doesn't appear to be a valid email address.")]
        public string EmailAddress { get; set; }

        public string PendingNewEmailAddress { get; set; }

        [Display(Name = "Receive Email Notifications")]
        public bool EmailAllowed { get; set; }
    }
}