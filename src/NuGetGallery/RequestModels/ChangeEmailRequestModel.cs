using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class ChangeEmailRequestModel
    {
        [Required]
        [StringLength(255)]
        [Display(Name = "New Email Address")]
        //[DataType(DataType.EmailAddress)] - does not work with client side validation
        [RegularExpression(RegisterRequest.EmailValidationRegex, ErrorMessage = RegisterRequest.EmailValidationErrorMessage)]
        public string NewEmail { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        [StringLength(64)]
        public string Password { get; set; }
    }
}