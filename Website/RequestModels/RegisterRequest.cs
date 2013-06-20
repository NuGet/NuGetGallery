using System.ComponentModel.DataAnnotations;
using NuGetGallery.Infrastructure;

namespace NuGetGallery
{
    public class RegisterRequest
    {
        // Note: regexes must be tested to work in javascript
        // (?<!\\.)
        internal const string FirstPart = @"[-A-Za-z0-9!#$%&'*+\/=?^_`{|}~\.]+";
        internal const string SecondPart = @"[A-Za-z0-9][\w\.-]*[A-Za-z0-9]\.[A-Za-z][A-Za-z\.]*[A-Za-z]";
        internal const string EmailValidationRegex ="^" + FirstPart + "@" + SecondPart + "$";

        internal const string EmailValidationErrorMessage = "This doesn't appear to be a valid email address.";

        internal const string UsernameValidationRegex =
            @"[A-Za-z0-9][A-Za-z0-9_.-]+[A-Za-z0-9]";

        internal const string UsernameValidationErrorMessage =
            "User names must start and end with a letter or number, and may only contain letters, numbers, underscores, periods, and hyphens in between.";

        [Required]
        [StringLength(255)]
        [Display(Name = "Email")]
        //[DataType(DataType.EmailAddress)] - does not work with client side validation
        [RegularExpression(EmailValidationRegex, ErrorMessage = EmailValidationErrorMessage)]
        [Hint(
            "Your email will not be public unless you choose to disclose it. " +
            "It is required to verify your registration and for password retrieval, important notifications, etc. ")]
        [Subtext("We use <a href=\"http://www.gravatar.com\" target=\"_blank\">Gravatar</a> to get your profile picture", AllowHtml = true)]
        public string EmailAddress { get; set; }

        [Required]
        [StringLength(64)]
        [RegularExpression(UsernameValidationRegex, ErrorMessage = UsernameValidationErrorMessage)]
        [Hint("Choose something unique so others will know which contributions are yours.")]
        public string Username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [StringLength(64, MinimumLength = 7)]
        [Hint("Passwords must be at least 7 characters long.")]
        public string Password { get; set; }

        [Required]
        [Compare("Password")]
        [DataType(DataType.Password)]
        [Display(Name = "Password Confirmation")]
        [Hint("Please reenter your password and ensure that it matches the one above.")]
        public string ConfirmPassword { get; set; }
    }
}