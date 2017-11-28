// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using NuGetGallery.Authentication.Providers;
using NuGetGallery.Infrastructure;

namespace NuGetGallery
{
    public class AccountViewModel
    {
        public AccountViewModel()
        {
            CuratedFeeds = new List<string>();
            ChangePassword = new ChangePasswordViewModel();
            ChangeEmail = new ChangeEmailViewModel();
            CredentialGroups = new Dictionary<CredentialKind, List<CredentialViewModel>>();
            ChangeNotifications = new ChangeNotificationsViewModel();
        }

        public IList<string> CuratedFeeds { get; set; }
        public ChangePasswordViewModel ChangePassword { get; set; }
        public ChangeEmailViewModel ChangeEmail { get; set; }
        public ChangeNotificationsViewModel ChangeNotifications { get; set; }
        public int ExpirationInDaysForApiKeyV1 { get; set; }
        public bool HasPassword { get; set; }
        public string CurrentEmailAddress { get; set; }
        public bool HasUnconfirmedEmailAddress { get; set; }
        public bool HasConfirmedEmailAddress { get; set; }
        public IDictionary<CredentialKind, List<CredentialViewModel>> CredentialGroups { get; set; }
        public int SignInCredentialCount { get; set; }
    }

    public class ChangeNotificationsViewModel
    {
        public bool EmailAllowed { get; set; }
        public bool NotifyPackagePushed { get; set; }
    }

    public class ChangeEmailViewModel
    {
        [Required]
        [StringLength(255)]
        [Display(Name = "New Email Address")]
        [RegularExpression(RegisterViewModel.EmailValidationRegex, ErrorMessage = RegisterViewModel.EmailValidationErrorMessage)]
        public string NewEmail { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        [StringLength(64)]
        [AllowHtml]
        public string Password { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required]
        [Display(Name = "Enable Password Login")]
        public bool EnablePasswordLogin { get; set; }

        [Required]
        [Display(Name = "Current Password")]
        [AllowHtml]
        public string OldPassword { get; set; }

        [Required]
        [Display(Name = "New Password")]
        [PasswordValidation]
        [AllowHtml]
        public string NewPassword { get; set; }

        [Required]
        [Display(Name = "Verify Password")]
        [PasswordValidation]
        [AllowHtml]
        public string VerifyPassword { get; set; }
    }
    
    public class CredentialViewModel
    {
        public int Key { get; set; }
        public string Type { get; set; }
        public string TypeCaption { get; set; }
        public string Identity { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Expires { get; set; }
        public CredentialKind Kind { get; set; }
        public AuthenticatorUI AuthUI { get; set; }
        public string Description { get; set; }
        public List<ScopeViewModel> Scopes { get; set; }
        public bool HasExpired { get; set; }
        public string Value { get; set; }
        public TimeSpan? ExpirationDuration { get; set; }

        public bool IsNonScopedV1ApiKey
        {
            get
            {
                return string.Equals(Type, CredentialTypes.ApiKey.V1, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    public enum CredentialKind
    {
        Password,
        Token,
        External
    }
}
