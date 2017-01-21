﻿// Copyright (c) .NET Foundation. All rights reserved.
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
            ChangePassword = new ChangePasswordViewModel();
            Packages = new List<string>();
        }

        public IEnumerable<string> CuratedFeeds { get; set; }
        public IList<CredentialViewModel> Credentials { get; set; }
        public List<string> Packages { get; set; }
        public ChangePasswordViewModel ChangePassword { get; set; }
        public ChangeEmailViewModel ChangeEmail { get; set; }
        public int ExpirationInDaysForApiKeyV1 { get; set; }
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
        [AllowHtml]
        public string Password { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required]
        [Display(Name = "Old Password")]
        [AllowHtml]
        public string OldPassword { get; set; }

        [Required]
        [Display(Name = "New Password")]
        [PasswordValidation]
        [AllowHtml]
        public string NewPassword { get; set; }
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
