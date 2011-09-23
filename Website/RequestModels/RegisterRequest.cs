using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace NuGetGallery {
    public class RegisterRequest {
        [Required]
        [StringLength(64)]
        //[RegularExpression(@"(?i)[a-z0-9_-]*", ErrorMessage = "User names may only contain letters, numbers, dashes, and hyphens.")]
        [AdditionalMetadata("Hint", "Choose something unique so others will know which contributions are yours.")]
        public string Username { get; set; }

        [Required]
        [StringLength(255)]
        [DisplayName("Email")]
        [DataType(DataType.EmailAddress)]
        //[RegularExpression(@"(?i)^(?!\.)(""([^""\r\\]|\\[""\r\\])*""|([-a-z0-9!#$%&'*+/=?^_`{|}~]|(?<!\.)\.)*)(?<!\.)@[a-z0-9][\w\.-]*[a-z0-9]\.[a-z][a-z\.]*[a-z]$", ErrorMessage="This doesn't appear to be a valid email address.")]
        [AdditionalMetadata("Hint", "Your email will not be public unless you choose to disclose it. It is required to verify your registration and for password retrieval, important notifications, etc.")]
        public string EmailAddress { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [StringLength(64, MinimumLength = 7)]
        [AdditionalMetadata("Hint", "Passwords must be at least 7 characters long.")]
        public string Password { get; set; }

        [Required]
        [Compare("Password")]
        [DataType(DataType.Password)]
        [DisplayName("Password Confirmation")]
        [AdditionalMetadata("Hint", "Please reenter your password and ensure that it matches the one above.")]
        public string ConfirmPassword { get; set; }
    }
}