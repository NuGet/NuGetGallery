using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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

        public Guid ApiKey { get; set; }

        [StringLength(256)]
        public string EmailAddress { get; set; }

        [StringLength(256)]
        public string UnconfirmedEmailAddress { get; set; }

        [StringLength(256)]
        public string HashedPassword { get; set; }

        // Would declare max length of this too, but EF is buggy, see http://entityframework.codeplex.com/workitem/452
        public string PasswordHashAlgorithm { get; set; }

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