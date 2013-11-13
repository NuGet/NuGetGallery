using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NuGetGallery.Auditing
{
    public class UserAuditRecord : AuditRecord
    {
        public string Username { get; set; }
        public string EmailAddress { get; set; }
        public string UnconfirmedEmailAddress { get; set; }
        public string[] Roles { get; set; }
        public CredentialAuditRecord[] Credentials { get; set; }
        public CredentialAuditRecord AffectedCredential { get; set; }
        public UserAuditAction Action { get; set; }

        public UserAuditRecord(User user, UserAuditAction action)
            : this(user, null, action) { }
        public UserAuditRecord(User user, Credential affected, UserAuditAction action)
        {
            Username = user.Username;
            EmailAddress = user.EmailAddress;
            UnconfirmedEmailAddress = user.UnconfirmedEmailAddress;
            Roles = user.Roles.Select(r => r.Name).ToArray();
            Credentials = user.Credentials.Select(c => new CredentialAuditRecord(c)).ToArray();

            if (affected != null)
            {
                AffectedCredential = new CredentialAuditRecord(affected);
            }

            Action = action;
        }

        public override string GetPath()
        {
            return String.Format(
                "{0}/{1}-{2}.json",
                Username.ToLowerInvariant(),
                DateTime.UtcNow.ToString("s"), // Sortable DateTime Format
                Action.ToString().ToLowerInvariant());
        }
    }

    public class CredentialAuditRecord
    {
        public string Type { get; set; }
        public string Identity { get; set; }

        public CredentialAuditRecord(Credential credential)
        {
            Type = credential.Type;
            Identity = credential.Identity;
        }
    }

    public enum UserAuditAction
    {
        Registered,
        ReplacedCredential,
        AddedCredential,
        RemovedCredential,
        MigratedCredential,
        RequestedPasswordReset,
    }
}
