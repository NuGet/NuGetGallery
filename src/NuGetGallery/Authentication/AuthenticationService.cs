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
using NuGetGallery.Configuration;
using Microsoft.Owin.Security;

namespace NuGetGallery.Authentication
{
    public class AuthenticationService
    {
        public IEntitiesContext Entities { get; private set; }
        public IAppConfiguration Config { get; private set; }
        private IDiagnosticsSource Trace { get; set; }

        protected AuthenticationService() { }

        public AuthenticationService(IEntitiesContext entities, IAppConfiguration config, IDiagnosticsService diagnostics)
        {
            Entities = entities;
            Config = config;
            Trace = diagnostics.SafeGetSource("AuthenticationService");
        }

        public virtual AuthenticatedUser Authenticate(string userNameOrEmail, string password)
        {
            using (Trace.Activity("Authenticate:" + userNameOrEmail))
            {
                var user = FindByUserNameOrEmail(userNameOrEmail);

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

                var passwordCredentials = user
                    .Credentials
                    .Where(c => c.Type.StartsWith(CredentialTypes.Password.Prefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (passwordCredentials.Count > 1 || !passwordCredentials.Any(c => String.Equals(c.Type, CredentialTypes.Password.Pbkdf2, StringComparison.OrdinalIgnoreCase)))
                {
                    MigrateCredentials(user, passwordCredentials, password);
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

        public virtual void CreateSession(IOwinContext owinContext, User user, string authenticationType)
        {
            // Create a claims identity for the session
            ClaimsIdentity identity = CreateIdentity(user, authenticationType);

            // Issue the session token
            owinContext.Authentication.SignIn(identity);
        }

        public virtual AuthenticatedUser Register(string username, string password, string emailAddress)
        {
            var existingUser = Entities.Users
                .FirstOrDefault(u => u.Username == username || u.EmailAddress == emailAddress);
            if (existingUser != null)
            {
                if (String.Equals(existingUser.Username, username, StringComparison.OrdinalIgnoreCase))
                {
                    throw new EntityException(Strings.UsernameNotAvailable, username);
                }
                else
                {
                    throw new EntityException(Strings.EmailAddressBeingUsed, emailAddress);
                }
            }

            var hashedPassword = CryptographyService.GenerateSaltedHash(password, Constants.PBKDF2HashAlgorithmId);

            var apiKey = Guid.NewGuid();
            var newUser = new User(username)
            {
                ApiKey = apiKey,
                EmailAllowed = true,
                UnconfirmedEmailAddress = emailAddress,
                EmailConfirmationToken = CryptographyService.GenerateToken(),
                HashedPassword = hashedPassword,
                PasswordHashAlgorithm = Constants.PBKDF2HashAlgorithmId,
                CreatedUtc = DateTime.UtcNow
            };

            // Add a credential for the password and the API Key
            var passCred = new Credential(CredentialTypes.Password.Pbkdf2, newUser.HashedPassword);
            newUser.Credentials.Add(CredentialBuilder.CreateV1ApiKey(apiKey));
            newUser.Credentials.Add(passCred);

            if (!Config.ConfirmEmailAddresses)
            {
                newUser.ConfirmEmailAddress();
            }

            Entities.Users.Add(newUser);
            Entities.SaveChanges();

            return new AuthenticatedUser(newUser, passCred);
        }

        public virtual void ReplaceCredential(string username, Credential credential)
        {
            var user = Entities
                .Users
                .Include(u => u.Credentials)
                .SingleOrDefault(u => u.Username == username);
            if (user == null)
            {
                throw new InvalidOperationException(Strings.UserNotFound);
            }
            ReplaceCredential(user, credential);
        }

        public virtual void ReplaceCredential(User user, Credential credential)
        {
            ReplaceCredentialInternal(user, credential);
            Entities.SaveChanges();
        }

        public virtual bool ResetPasswordWithToken(string username, string token, string newPassword)
        {
            if (String.IsNullOrEmpty(newPassword))
            {
                throw new ArgumentNullException("newPassword");
            }

            var user = Entities
                .Users
                .Include(u => u.Credentials)
                .SingleOrDefault(u => u.Username == username);

            if (user != null && String.Equals(user.PasswordResetToken, token, StringComparison.Ordinal) && !user.PasswordResetTokenExpirationDate.IsInThePast())
            {
                if (!user.Confirmed)
                {
                    throw new InvalidOperationException(Strings.UserIsNotYetConfirmed);
                }

                ReplaceCredentialInternal(user, CredentialBuilder.CreatePbkdf2Password(newPassword));
                user.PasswordResetToken = null;
                user.PasswordResetTokenExpirationDate = null;
                Entities.SaveChanges();
                return true;
            }

            return false;
        }

        public virtual bool ChangePassword(string username, string oldPassword, string newPassword)
        {
            // Review: If the old password is hashed using something other than PBKDF2, we end up making an extra db call that changes the old hash password.
            // This operation is rare enough that I'm not inclined to change it.
            var authUser = Authenticate(username, oldPassword);
            if (authUser == null)
            {
                return false;
            }

            var cred = CredentialBuilder.CreatePbkdf2Password(newPassword);
            ReplaceCredentialInternal(authUser.User, cred);
            Entities.SaveChanges();
            return true;
        }

        public virtual User GeneratePasswordResetToken(string usernameOrEmail, int expirationInMinutes)
        {
            if (String.IsNullOrEmpty(usernameOrEmail))
            {
                throw new ArgumentNullException("usernameOrEmail");
            }
            if (expirationInMinutes < 1)
            {
                throw new ArgumentException(
                    "Token expiration should give the user at least a minute to change their password", "expirationInMinutes");
            }

            var user = FindByUserNameOrEmail(usernameOrEmail);
            if (user == null)
            {
                return null;
            }

            if (!user.Confirmed)
            {
                throw new InvalidOperationException(Strings.UserIsNotYetConfirmed);
            }

            if (!String.IsNullOrEmpty(user.PasswordResetToken) && !user.PasswordResetTokenExpirationDate.IsInThePast())
            {
                return user;
            }

            user.PasswordResetToken = CryptographyService.GenerateToken();
            user.PasswordResetTokenExpirationDate = DateTime.UtcNow.AddMinutes(expirationInMinutes);

            Entities.SaveChanges();
            return user;
        }

        public static ClaimsIdentity CreateIdentity(User user, string authenticationType, params Claim[] additionalClaims)
        {
            var claims = Enumerable.Concat(new[] {
                new Claim(ClaimsIdentity.DefaultNameClaimType, user.Username),
                new Claim(ClaimTypes.AuthenticationMethod, authenticationType),

                // Needed for anti-forgery token, also good practice to have a unique identifier claim
                new Claim(ClaimTypes.NameIdentifier, user.Username)
            }, user.Roles.Select(r => new Claim(ClaimsIdentity.DefaultRoleClaimType, r.Name)));

            if (additionalClaims.Length > 0)
            {
                claims = Enumerable.Concat(claims, additionalClaims);
            }

            ClaimsIdentity identity = new ClaimsIdentity(
                claims,
                authenticationType,
                nameType: ClaimsIdentity.DefaultNameClaimType,
                roleType: ClaimsIdentity.DefaultRoleClaimType);
            return identity;
        }

        private void ReplaceCredentialInternal(User user, Credential credential)
        {
            // Find the credentials we're replacing, if any
            var creds = user.Credentials
                .Where(cred =>
                    // If we're replacing a password credential, remove ALL password credentials
                    (credential.Type.StartsWith(CredentialTypes.Password.Prefix, StringComparison.OrdinalIgnoreCase) &&
                     cred.Type.StartsWith(CredentialTypes.Password.Prefix, StringComparison.OrdinalIgnoreCase)) ||
                    cred.Type == credential.Type)
                .ToList();
            foreach (var cred in creds)
            {
                user.Credentials.Remove(cred);
                Entities.DeleteOnCommit(cred);
            }

            user.Credentials.Add(credential);
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

        private User FindByUserNameOrEmail(string userNameOrEmail)
        {
            var users = Entities
                .Users
                .Include(u => u.Credentials)
                .Include(u => u.Roles);

            var user = users.SingleOrDefault(u => u.Username == userNameOrEmail);
            if (user == null)
            {
                var allMatches = users
                    .Where(u => u.EmailAddress == userNameOrEmail)
                    .Take(2)
                    .ToList();

                if (allMatches.Count == 1)
                {
                    user = allMatches[0];
                }
                else
                {
                    // If multiple matches, leave it null to signal no unique email address
                    Trace.Warning("Multiple user accounts with email address: " + userNameOrEmail + " found: " + String.Join(", ", allMatches.Select(u => u.Username)));
                }
            }
            return user;
        }

        public static bool ValidatePasswordCredential(IEnumerable<Credential> creds, string password, out Credential matched)
        {
            matched = creds.FirstOrDefault(c => ValidatePasswordCredential(c, password));
            return matched != null;
        }

        private static readonly Dictionary<string, Func<string, Credential, bool>> _validators = new Dictionary<string, Func<string, Credential, bool>>(StringComparer.OrdinalIgnoreCase) {
            { CredentialTypes.Password.Pbkdf2, (password, cred) => CryptographyService.ValidateSaltedHash(cred.Value, password, Constants.PBKDF2HashAlgorithmId) },
            { CredentialTypes.Password.Sha1, (password, cred) => CryptographyService.ValidateSaltedHash(cred.Value, password, Constants.Sha1HashAlgorithmId) }
        };

        public static bool ValidatePasswordCredential(Credential cred, string password)
        {
            Func<string, Credential, bool> validator;
            if (!_validators.TryGetValue(cred.Type, out validator))
            {
                return false;
            }
            return validator(password, cred);
        }

        private void MigrateCredentials(User user, List<Credential> creds, string password)
        {
            var toRemove = creds.Where(c =>
                !String.Equals(
                    c.Type,
                    CredentialTypes.Password.Pbkdf2,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Remove any non PBKDF2 credentials
            foreach (var cred in toRemove)
            {
                creds.Remove(cred);
                user.Credentials.Remove(cred);
            }

            // Now add one if there are no credentials left
            if (creds.Count == 0)
            {
                user.Credentials.Add(CredentialBuilder.CreatePbkdf2Password(password));
            }

            // Save changes, if any
            Entities.SaveChanges();
        }
    }
}