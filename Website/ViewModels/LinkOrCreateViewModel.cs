﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Web.Security;

namespace NuGetGallery
{
    public class LinkOrCreateViewModel
    {
        public LinkViewModel LinkModel { get; set; }
        public CreateViewModel CreateModel { get; set; }

        // [anurse] These are only ever used here, so it makes sense (to me at least) that they be nested
        public class LinkViewModel
        {
            [Required]
            [Display(Name = "Username or Email")]
            [Hint("Enter your username or email address.")]
            public string UserNameOrEmail { get; set; }

            [Required]
            [DataType(DataType.Password)]
            [Hint("Passwords must be at least 7 characters long.")]
            public string Password { get; set; }

            public override bool Equals(object obj)
            {
                LinkViewModel other = obj as LinkViewModel;
                return other != null && 
                    String.Equals(UserNameOrEmail, other.UserNameOrEmail) &&
                    String.Equals(Password, other.Password);
            }

            // Silence the compiler warning
            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }

        public class CreateViewModel
        {
            [Required]
            [StringLength(255)]
            [Display(Name = "Email")]
            [DataType(DataType.EmailAddress)]
            [RegularExpression(
                @"(?i)^(?!\.)(""([^""\r\\]|\\[""\r\\])*""|([-a-z0-9!#$%&'*+/=?^_`{|}~]|(?<!\.)\.)*)(?<!\.)@[a-z0-9][\w\.-]*[a-z0-9]\.[a-z][a-z\.]*[a-z]$",
                ErrorMessage = "This doesn't appear to be a valid email address.")]
            [Hint(
                "Your email will not be public unless you choose to disclose it. It is required to verify your registration and for password retrieval, important notifications, etc."
                )]
            public string EmailAddress { get; set; }

            [Required]
            [StringLength(64)]
            [RegularExpression(@"(?i)[a-z0-9][a-z0-9_.-]+[a-z0-9]",
                ErrorMessage =
                    "User names must start and end with a letter or number, and may only contain letters, numbers, underscores, periods, and hyphens in between."
                )]
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

            public override bool Equals(object obj)
            {
                CreateViewModel other = obj as CreateViewModel;
                return other != null &&
                    String.Equals(EmailAddress, other.EmailAddress) &&
                    String.Equals(Username, other.Username) &&
                    String.Equals(Password, other.Password) &&
                    String.Equals(ConfirmPassword, other.ConfirmPassword);
            }

            // Silence the compiler warning
            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }

        public override bool Equals(object obj)
        {
            LinkOrCreateViewModel other = obj as LinkOrCreateViewModel;
            return other != null &&
                Object.Equals(LinkModel, other.LinkModel) &&
                Object.Equals(CreateModel, other.CreateModel);
        }

        // Silence the compiler warning
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
