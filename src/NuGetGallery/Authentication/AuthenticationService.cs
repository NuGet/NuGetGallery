using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NuGetGallery.Diagnostics;

namespace NuGetGallery.Authentication
{
    public class AuthenticationService
    {
        public IEntitiesContext Entities { get; private set; }

        private IDiagnosticsSource Trace { get; set; }

        public AuthenticationService(IEntitiesContext entities, IDiagnosticsService diagnostics)
        {
            Entities = entities;
            Trace = diagnostics.SafeGetSource("AuthenticationService");
        }

        public virtual AuthenticateUserResult AuthenticateUser(string userNameOrEmail, string password)
        {
            using (Trace.Activity("Authenticate:" + userNameOrEmail))
            {
                var user = FindByUserName(userNameOrEmail);

                // Check if the user exists
                if (user == null)
                {
                    Trace.Information("No such user: " + userNameOrEmail);
                    return AuthenticateUserResult.Failed;
                }

                // Validate the password
                Credential matched;
                if (!ValidatePasswordCredential(user.Credentials, password, out matched))
                {
                    Trace.Information("Password validation failed: " + userNameOrEmail);
                    return AuthenticateUserResult.Failed;
                }

                // Return the result
                Trace.Verbose("Successfully authenticated '" + user.Username + "' with '" + matched.Type + "' credential");
                return new AuthenticateUserResult(user);
            }
        }

        private User FindByUserName(string userNameOrEmail)
        {
            return Entities
                .Set<User>()
                .SingleOrDefault(u => u.Username == userNameOrEmail);
        }

        private static bool ValidatePasswordCredential(IEnumerable<Credential> creds, string password, out Credential matched)
        {
            matched = creds.FirstOrDefault(c => ValidatePasswordCredential(c, password));
            return matched != null;
        }

        private static readonly Dictionary<string, Func<string, Credential, bool>> _validators = new Dictionary<string, Func<string, Credential, bool>>(StringComparer.OrdinalIgnoreCase) {
            { CredentialTypes.Password.Pbkdf2, (password, cred) => CryptographyService.ValidateSaltedHash(cred.Value, password, Constants.PBKDF2HashAlgorithmId) },
            { CredentialTypes.Password.Sha1, (password, cred) => CryptographyService.ValidateSaltedHash(cred.Value, password, Constants.Sha1HashAlgorithmId) }
        };

        private static bool ValidatePasswordCredential(Credential cred, string password)
        {
            Func<string, Credential, bool> validator;
            if (!_validators.TryGetValue(cred.Type, out validator))
            {
                return false;
            }
            return validator(password, cred);
        }
    }
}