using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class User : IEntity
    {
        public User()
            : this(null, null)
        {
        }

        public User(
            string username,
            string hashedPassword)
        {
            HashedPassword = hashedPassword;
            Messages = new HashSet<EmailMessage>();
            Username = username;
        }

        public int Key { get; set; }

        public Guid ApiKey { get; set; }
        public string EmailAddress { get; set; }
        public string UnconfirmedEmailAddress { get; set; }
        public string HashedPassword { get; set; }
        public string PasswordHashAlgorithm { get; set; }
        public virtual ICollection<EmailMessage> Messages { get; set; }
        public string Username { get; set; }
        public virtual ICollection<Role> Roles { get; set; }
        public bool EmailAllowed { get; set; }
        public bool Confirmed
        {
            get
            {
                return !String.IsNullOrEmpty(EmailAddress);
            }
        }
        public string EmailConfirmationToken { get; set; }
        public string PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenExpirationDate { get; set; }

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
    }
}