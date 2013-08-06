using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class ResendConfirmationEmailViewModel
    {
        [Required]
        [Display(Name = "Email")]
        //[DataType(DataType.EmailAddress)] - does not work with client side validation
        [RegularExpression(RegisterRequest.EmailValidationRegex, ErrorMessage = RegisterRequest.EmailValidationErrorMessage)]
        public string Email { get; set; }

        [RegularExpression(RegisterRequest.UsernameValidationRegex, ErrorMessage = RegisterRequest.UsernameValidationErrorMessage)]
        public string Username { get; set; }
    }
}