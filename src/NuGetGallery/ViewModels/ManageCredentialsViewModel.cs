using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using NuGetGallery.Authentication.Providers;

namespace NuGetGallery
{
    public class ManageCredentialsViewModel
    {
        public bool UserConfirmed { get; set; }
        public IList<CredentialViewModel> Credentials { get; set; }

        [Required]
        [Display(Name = "Old Password")]
        public string OldPassword { get; set; }

        [Required]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; }

        public ManageCredentialsViewModel() { }
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