// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using NuGetGallery.Authentication.Providers;
using NuGetGallery.Infrastructure;

namespace NuGetGallery
{
    // I moved these in to one file because they are so interconnected it didn't
    // make sense to look at them separately
    //  - anurse

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
        // Note: regexes must be tested to work in javascript
        // We do NOT follow strictly the RFCs at this time, and we choose not to support many obscure email address variants.
        // Specifically the following are not supported by-design:
        //  * Addresses containing () or []
        //  * Second parts with no dots (i.e. foo@localhost or foo@com)
        //  * Addresses with quoted (" or ') first parts
        //  * Addresses with IP Address second parts (foo@[127.0.0.1])
        internal const string FirstPart = @"[-A-Za-z0-9!#$%&'*+\/=?^_`{|}~\.]+";
        internal const string SecondPart = @"[A-Za-z0-9]+[\w\.-]*[A-Za-z0-9]*\.[A-Za-z0-9][A-Za-z\.]*[A-Za-z]";
        internal const string EmailValidationRegex = "^" + FirstPart + "@" + SecondPart + "$";

        internal const string EmailValidationErrorMessage = "This doesn't appear to be a valid email address.";
        public const string EmailHint = "Your email will not be public unless you choose to disclose it. " +
                                          "It is required to verify your registration and for password retrieval, important notifications, etc. ";

        internal const string UsernameValidationRegex =
            @"[A-Za-z0-9][A-Za-z0-9_\.-]+[A-Za-z0-9]";
        
        internal const string UsernameValidationErrorMessage =
            "User names must start and end with a letter or number, and may only contain letters, numbers, underscores, periods, and hyphens in between.";

        public const string UserNameHint = "Choose something unique so others will know which contributions are yours.";

        [Required]
        [StringLength(255)]
        [Display(Name = "Email")]
        [RegularExpression(EmailValidationRegex, ErrorMessage = EmailValidationErrorMessage)]
        [Subtext("We use <a href=\"http://www.gravatar.com\" target=\"_blank\">Gravatar</a> to get your profile picture", AllowHtml = true)]
        public string EmailAddress { get; set; }

        [Required]
        [StringLength(64)]
        [RegularExpression(UsernameValidationRegex, ErrorMessage = UsernameValidationErrorMessage)]
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