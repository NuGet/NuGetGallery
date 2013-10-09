using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;
using NuGetGallery.Diagnostics;
using System.Data.Entity;
using System.Globalization;
using Microsoft.Owin;
using System.Security.Claims;

namespace NuGetGallery.Authentication
{
    public class AuthenticationService
    {
        public IEntitiesContext Entities { get; private set; }
        public IOwinContext Context { get; private set; }
        private IDiagnosticsSource Trace { get; set; }

        public AuthenticationService(IEntitiesContext entities, IOwinContext context, IDiagnosticsService diagnostics)
        {
            Entities = entities;
            Context = context;
            Trace = diagnostics.SafeGetSource("AuthenticationService");
        }

        public virtual AuthenticatedUser Authenticate(string userNameOrEmail, string password)
        {
            using (Trace.Activity("Authenticate:" + userNameOrEmail))
            {
                var user = FindByUserName(userNameOrEmail);

                // Check if the user exists
                if (user == null)
                {
                    Trace.Information("No such user: " + userNameOrEmail);
                    return null;
                }

                // Validate the password
                Credential matched;
                if (!ValidatePasswordCredential(user.Credentials, password, out matched))
                {
                    Trace.Information("Password validation failed: " + userNameOrEmail);
                    return null;
                }

                // Return the result
                Trace.Verbose("Successfully authenticated '" + user.Username + "' with '" + matched.Type + "' credential");
                return new AuthenticatedUser(user, matched);
            }
        }

        public virtual AuthenticatedUser Authenticate(Credential credential)
        {
            if (credential.Type.StartsWith(CredentialTypes.Password.Prefix, StringComparison.OrdinalIgnoreCase))
            {
                // Password credentials cannot be used this way.
                throw new ArgumentException(Strings.PasswordCredentialsCannotBeUsedHere, "credential");
            }

            using (Trace.Activity("Authenticate Credential: " + credential.Type))
            {
                var matched = FindMatchingCredential(credential);

                if (matched == null)
                {
                    Trace.Information("No user matches credential of type: " + credential.Type);
                    return null;
                }

                Trace.Verbose("Successfully authenticated '" + matched.User.Username + "' with '" + matched.Type + "' credential");
                return new AuthenticatedUser(matched.User, matched);
            }
        }

        public virtual void CreateSession(AuthenticatedUser user)
        {
            // Create a claims identity for the session
            ClaimsIdentity identity = CreateIdentity(user);

            Context.Authentication.SignIn(identity);
        }

        public static ClaimsIdentity CreateIdentity(AuthenticatedUser user)
        {
            return CreateIdentity(user.User, user.CredentialUsed.Type);
        }
        
        public static ClaimsIdentity CreateIdentity(User user, string authenticationType)
        {
            ClaimsIdentity identity = new ClaimsIdentity(
                claims: Enumerable.Concat(new[] {
                    new Claim(ClaimsIdentity.DefaultNameClaimType, user.Username)
                }, user.Roles.Select(r => new Claim(ClaimsIdentity.DefaultRoleClaimType, r.Name))),
                authenticationType: authenticationType,
                nameType: ClaimsIdentity.DefaultNameClaimType,
                roleType: ClaimsIdentity.DefaultRoleClaimType);
            return identity;
        }

        private Credential FindMatchingCredential(Credential credential)
        {
            var results = Entities
                .Set<Credential>()
                .Include(u => u.User)
                .Include(u => u.User.Roles)
                .Where(c => c.Type == credential.Type && c.Value == credential.Value)
                .ToList();
            
            if (results.Count == 0)
            {
                return null;
            }
            else if (results.Count == 1)
            {
                return results[0];
            }
            else
            {
                // Don't put the credential itself in trace, but do put the Key for lookup later.
                string message = String.Format(
                    CultureInfo.CurrentCulture,
                    Strings.MultipleMatchingCredentials,
                    credential.Type,
                    results.First().Key);
                Trace.Error(message);
                throw new InvalidOperationException(message);
            }
        }

        private User FindByUserName(string userNameOrEmail)
        {
            return Entities
                .Set<User>()
                .Include(u => u.Credentials)
                .Include(u => u.Roles)
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