// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class User : IEntity
    {
        public User() : this(null)
        {
        }

        public User(string username)
        {
            Credentials = new List<Credential>();
            Roles = new List<Role>();
            Username = username;
        }

        [StringLength(256)]
        public string EmailAddress { get; set; }

        [StringLength(256)]
        public string UnconfirmedEmailAddress { get; set; }

        public virtual ICollection<EmailMessage> Messages { get; set; }

        [StringLength(64)]
        [Required]
        public string Username { get; set; }

        public virtual ICollection<Role> Roles { get; set; }
        public bool EmailAllowed { get; set; }

        public bool Confirmed
        {
            get { return !String.IsNullOrEmpty(EmailAddress); }
        }

        [StringLength(32)]
        public string EmailConfirmationToken { get; set; }

        [StringLength(32)]
        public string PasswordResetToken { get; set; }

        public DateTime? PasswordResetTokenExpirationDate { get; set; }
        public int Key { get; set; }

        public DateTime? CreatedUtc { get; set; }

        public string LastSavedEmailAddress
        {
            get
            {
                return UnconfirmedEmailAddress ?? EmailAddress;
            }
        }
        public virtual ICollection<Credential> Credentials { get; set; }

        public void ConfirmEmailAddress()
        {
            if (String.IsNullOrEmpty(UnconfirmedEmailAddress))
            {
                throw new InvalidOperationException("User does not have an email address to confirm");
            }
            EmailAddress = UnconfirmedEmailAddress;
            EmailConfirmationToken = null;
            UnconfirmedEmailAddress = null;
        }

        public void CancelChangeEmailAddress()
        {
            EmailConfirmationToken = null;
            UnconfirmedEmailAddress = null;
        }

        public void UpdateEmailAddress(string newEmailAddress, Func<string> generateToken)
        {
            if (!String.IsNullOrEmpty(UnconfirmedEmailAddress))
            {
                if (String.Equals(UnconfirmedEmailAddress, newEmailAddress, StringComparison.Ordinal))
                {
                    return; // already set as latest (unconfirmed) email address
                }
            }
            else
            {
                if (String.Equals(EmailAddress, newEmailAddress, StringComparison.Ordinal))
                {
                    return; // already set as latest (confirmed) email address
                }
            }

            UnconfirmedEmailAddress = newEmailAddress;
            EmailConfirmationToken = generateToken();
        }

        public bool HasPassword()
        {
            return Credentials.Any(c =>
                c.Type.StartsWith(CredentialTypes.Password.Prefix, StringComparison.OrdinalIgnoreCase));
        }
    }
}
