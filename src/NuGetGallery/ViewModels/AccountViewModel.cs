using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NuGetGallery.Authentication.Providers;

namespace NuGetGallery
{
    public class AccountViewModel
    {
        public IEnumerable<string> CuratedFeeds { get; set; }
        public IList<CredentialViewModel> Credentials { get; set; }
        public ChangePasswordViewModel ChangePassword { get; set; }
        public ChangeEmailViewModel ChangeEmail { get; set; }
    }

    public class ChangeEmailViewModel
    {
        [Required]
        [StringLength(255)]
        [Display(Name = "New Email Address")]
        //[DataType(DataType.EmailAddress)] - does not work with client side validation
        [RegularExpression(RegisterViewModel.EmailValidationRegex, ErrorMessage = RegisterViewModel.EmailValidationErrorMessage)]
        public string NewEmail { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password (for verification)")]
        [StringLength(64)]
        public string Password { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required]
        [Display(Name = "Old Password")]
        public string OldPassword { get; set; }

        [Required]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; }
    }

    public class CredentialViewModel
    {
        public string Type { get; set; }
        public string TypeCaption { get; set; }
        public string Identity { get; set; }
        public string Value { get; set; }
        public CredentialKind Kind { get; set; }
        public AuthenticatorUI AuthUI { get; set; }
    }

    public enum CredentialKind
    {
        Password,
        Token,
        External
    }
}