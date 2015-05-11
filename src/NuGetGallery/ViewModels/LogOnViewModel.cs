// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Web;
using NuGetGallery.Authentication.Providers;

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

        public LogOnViewModel() { }

        public LogOnViewModel(SignInViewModel signIn)
        {
            SignIn = signIn;
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

        internal const string UsernameValidationRegex =
            @"[A-Za-z0-9][A-Za-z0-9_\.-]+[A-Za-z0-9]";

        /// <summary>
        /// Regex that matches INVALID username characters, to make it easy to strip those characters out.
        /// </summary>
        internal static readonly Regex UsernameNormalizationRegex =
            new Regex(@"[^A-Za-z0-9_\.-]");

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
    }

    public class AuthenticationProviderViewModel
    {
        public string ProviderName { get; set; }
        public AuthenticatorUI UI { get; set; }
    }
}