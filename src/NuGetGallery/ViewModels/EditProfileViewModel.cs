using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NuGetGallery.Authentication.Providers;

namespace NuGetGallery
{
    public class EditProfileViewModel
    {
        public IList<CredentialViewModel> Credentials { get; set; }
        public ChangePasswordViewModel ChangePassword { get; set; }
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