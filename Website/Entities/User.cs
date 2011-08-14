using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery {
    public class User : IEntity {
        public User()
            : this(null, null, null) {
        }

        public User(
            string username,
            string hashedPassword,
            string emailAddress) {
            EmailAddress = emailAddress;
            HashedPassword = hashedPassword;
            Messages = new HashSet<EmailMessage>();
            Username = username;
        }

        public int Key { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public Guid ApiKey { get; set; }
        public string EmailAddress { get; set; }
        public string HashedPassword { get; set; }
        public virtual ICollection<EmailMessage> Messages { get; set; }
        public string Username { get; set; }
    }
}