// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using NuGetGallery.Authentication.Providers;
using NuGetGallery.Infrastructure;

namespace NuGetGallery
{
    public class LogOnViewModel
    {
        public AssociateExternalAccountViewModel External { get; set; }
        public SignInViewModel SignIn { get; set; }
        public RegisterViewModel Register { get; set; }
        public IList<AuthenticationProviderViewModel> Providers { get; set; }

        public LogOnViewModel()
            : this(new SignInViewModel())
        {
        }

        internal LogOnViewModel(SignInViewModel signIn)
        {
            SignIn = signIn;
            Register = new RegisterViewModel();
        }
    }

    public class AssociateExternalAccountViewModel
    {
        public string ProviderAccountNoun { get; set; }
        public string AccountName { get; set; }
        public bool FoundExistingUser { get; set; }
        public bool ExistingUserCanBeLinked => ExistingUserLinkingError == ExistingUserLinkingErrorType.None;
        public ExistingUserLinkingErrorType ExistingUserLinkingError { get; set; }
        public bool UsedMultiFactorAuthentication { get; set; }

        public enum ExistingUserLinkingErrorType
        {
            None = (int)default(ExistingUserLinkingErrorType),
            AccountIsOrganization,
            AccountIsAlreadyLinked
        }
    }

    public class SignInViewModel
    {
        [Required]
        [Display(Name = "Username or Email")]
        [Hint("Enter your username or email address.")]
        public string UserNameOrEmail { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Hint("Passwords must be at least 7 characters long.")]
        [AllowHtml]
        public string Password { get; set; }

        public SignInViewModel() { }
        public SignInViewModel(string userNameOrEmail, string password)
        {
            UserNameOrEmail = userNameOrEmail;
            Password = password;
        }
    }

    public class RegisterViewModel
    {
        public const string EmailHint = "Your email will not be public unless you choose to disclose it. " +
                                          "It is required to verify your registration and for password retrieval, important notifications, etc. ";

        public const string UserNameHint = "Choose something unique so others will know which contributions are yours.";

        [Required]
        [StringLength(255)]
        [Display(Name = "Email")]
        [RegularExpression(GalleryConstants.EmailValidationRegex, ErrorMessage = GalleryConstants.EmailValidationErrorMessage)]
        [Subtext("We use <a href=\"http://www.gravatar.com\" target=\"_blank\">Gravatar</a> to get your profile picture", AllowHtml = true)]
        public string EmailAddress { get; set; }

        [Required]
        [StringLength(64)]
        [RegularExpression(GalleryConstants.UsernameValidationRegex, ErrorMessage = GalleryConstants.UsernameValidationErrorMessage)]
        public string Username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [PasswordValidation]
        [AllowHtml]
        public string Password { get; set; }
    }

    public class AuthenticationProviderViewModel
    {
        public string ProviderName { get; set; }
        public AuthenticatorUI UI { get; set; }
    }
}