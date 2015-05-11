// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
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
using NuGetGallery.Authentication.Providers;
using System.Web.Mvc;
using System.Threading.Tasks;
using NuGetGallery.Auditing;

namespace NuGetGallery.Authentication
{
    public class AuthenticationService
    {
        public IEntitiesContext Entities { get; private set; }
        public IAppConfiguration Config { get; private set; }
        public IDictionary<string, Authenticator> Authenticators { get; private set; }
        public AuditingService Auditing { get; private set; }

        private IDiagnosticsSource Trace { get; set; }

        private readonly Dictionary<string, Func<string, string>> _credentialFormatters;

        protected AuthenticationService()
            : this(null, null, null, AuditingService.None, Enumerable.Empty<Authenticator>())
        {
        }

        public AuthenticationService(IEntitiesContext entities, IAppConfiguration config, IDiagnosticsService diagnostics, AuditingService auditing, IEnumerable<Authenticator> providers)
        {
            _credentialFormatters = new Dictionary<string, Func<string, string>>(StringComparer.OrdinalIgnoreCase) {
                { "password", _ => Strings.CredentialType_Password },
                { "apikey", _ => Strings.CredentialType_ApiKey },
                { "external", FormatExternalCredentialType }
            };

            Entities = entities;
            Config = config;
            Auditing = auditing;
            Trace = diagnostics.SafeGetSource("AuthenticationService");
            Authenticators = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        }

        public virtual async Task<AuthenticatedUser> Authenticate(string userNameOrEmail, string password)
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
                    await MigrateCredentials(user, passwordCredentials, password);
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

        public virtual void CreateSession(IOwinContext owinContext, User user)
        {
            // Create a claims identity for the session
            ClaimsIdentity identity = CreateIdentity(user, AuthenticationTypes.LocalUser);

            // Issue the session token and clean up the external token if present
            owinContext.Authentication.SignIn(identity);
            owinContext.Authentication.SignOut(AuthenticationTypes.External);
        }

        public virtual async Task<AuthenticatedUser> Register(string username, string emailAddress, Credential credential)
        {
            if (Config.FeedOnlyMode)
            {
                throw new FeedOnlyModeException(FeedOnlyModeException.FeedOnlyModeError);
            }

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

            var apiKey = Guid.NewGuid();
            var newUser = new User(username)
            {
                EmailAllowed = true,
                UnconfirmedEmailAddress = emailAddress,
                EmailConfirmationToken = CryptographyService.GenerateToken(),
                CreatedUtc = DateTime.UtcNow
            };

            // Add a credential for the password and the API Key
            newUser.Credentials.Add(CredentialBuilder.CreateV1ApiKey(apiKey));
            newUser.Credentials.Add(credential);

            if (!Config.ConfirmEmailAddresses)
            {
                newUser.ConfirmEmailAddress();
            }

            // Write an audit record
            await Auditing.SaveAuditRecord(new UserAuditRecord(newUser, UserAuditAction.Registered));

            Entities.Users.Add(newUser);
            Entities.SaveChanges();

            return new AuthenticatedUser(newUser, credential);
        }

        [Obsolete("Use Register(string, string, Credential) now")]
        public virtual Task<AuthenticatedUser> Register(string username, string password, string emailAddress)
        {
            var hashedPassword = CryptographyService.GenerateSaltedHash(password, Constants.PBKDF2HashAlgorithmId);
            var passCred = new Credential(CredentialTypes.Password.Pbkdf2, hashedPassword);
            return Register(username, emailAddress, passCred);
        }

        public virtual Task ReplaceCredential(string username, Credential credential)
        {
            var user = Entities
                .Users
                .Include(u => u.Credentials)
                .SingleOrDefault(u => u.Username == username);
            if (user == null)
            {
                throw new InvalidOperationException(Strings.UserNotFound);
            }
            return ReplaceCredential(user, credential);
        }

        public virtual async Task ReplaceCredential(User user, Credential credential)
        {
            await ReplaceCredentialInternal(user, credential);
            Entities.SaveChanges();
        }

        public virtual async Task<Credential> ResetPasswordWithToken(string username, string token, string newPassword)
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

                var cred = CredentialBuilder.CreatePbkdf2Password(newPassword);
                await ReplaceCredentialInternal(user, cred);
                user.PasswordResetToken = null;
                user.PasswordResetTokenExpirationDate = null;
                Entities.SaveChanges();
                return cred;
            }

            return null;
        }

        public virtual async Task<User> GeneratePasswordResetToken(string usernameOrEmail, int expirationInMinutes)
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
            await GeneratePasswordResetToken(user, expirationInMinutes);
            return user;
        }

        public virtual async Task GeneratePasswordResetToken(User user, int expirationInMinutes)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            if (expirationInMinutes < 1)
            {
                throw new ArgumentException(
                    "Token expiration should give the user at least a minute to change their password", "expirationInMinutes");
            }

            if (!user.Confirmed)
            {
                throw new InvalidOperationException(Strings.UserIsNotYetConfirmed);
            }

            if (!String.IsNullOrEmpty(user.PasswordResetToken) && !user.PasswordResetTokenExpirationDate.IsInThePast())
            {
                return;
            }

            user.PasswordResetToken = CryptographyService.GenerateToken();
            user.PasswordResetTokenExpirationDate = DateTime.UtcNow.AddMinutes(expirationInMinutes);

            await Auditing.SaveAuditRecord(new UserAuditRecord(user, UserAuditAction.RequestedPasswordReset));

            Entities.SaveChanges();
            return;
        }

        public virtual async Task<bool> ChangePassword(User user, string oldPassword, string newPassword)
        {
            var hasPassword = user.Credentials.Any(
                c => c.Type.StartsWith(CredentialTypes.Password.Prefix, StringComparison.OrdinalIgnoreCase));
            Credential _;
            if (hasPassword && !ValidatePasswordCredential(user.Credentials, oldPassword, out _))
            {
                // Invalid old password!
                return false;
            }

            // Replace/Set password credential
            var cred = CredentialBuilder.CreatePbkdf2Password(newPassword);
            await ReplaceCredentialInternal(user, cred);
            Entities.SaveChanges();
            return true;
        }

        public virtual ActionResult Challenge(string providerName, string redirectUrl)
        {
            Authenticator provider;
            if (!Authenticators.TryGetValue(providerName, out provider))
            {
                throw new InvalidOperationException(String.Format(
                    CultureInfo.CurrentCulture,
                    Strings.UnknownAuthenticationProvider,
                    providerName));
            }
            if (!provider.BaseConfig.Enabled)
            {
                throw new InvalidOperationException(String.Format(
                    CultureInfo.CurrentCulture,
                    Strings.AuthenticationProviderDisabled,
                    providerName));
            }

            return provider.Challenge(redirectUrl);
        }

        public virtual async Task AddCredential(User user, Credential credential)
        {
            await Auditing.SaveAuditRecord(new UserAuditRecord(user, UserAuditAction.AddedCredential, credential));
            user.Credentials.Add(credential);
            Entities.SaveChanges();
        }

        public virtual CredentialViewModel DescribeCredential(Credential credential)
        {
            var kind = GetCredentialKind(credential.Type);
            Authenticator auther = null;
            if (kind == CredentialKind.External)
            {
                string providerName = credential.Type.Split('.')[1];
                if (!Authenticators.TryGetValue(providerName, out auther))
                {
                    auther = null;
                }
            }

            return new CredentialViewModel()
            {
                Type = credential.Type,
                TypeCaption = FormatCredentialType(credential.Type),
                Identity = credential.Identity,
                Value = kind == CredentialKind.Token ? credential.Value : String.Empty,
                Kind = kind,
                AuthUI = auther == null ? null : auther.GetUI()
            };
        }

        public virtual async Task RemoveCredential(User user, Credential cred)
        {
            await Auditing.SaveAuditRecord(new UserAuditRecord(user, UserAuditAction.RemovedCredential, cred));
            user.Credentials.Remove(cred);
            Entities.Credentials.Remove(cred);
            Entities.SaveChanges();
        }

        public async virtual Task<AuthenticateExternalLoginResult> ReadExternalLoginCredential(IOwinContext context)
        {
            var result = await context.Authentication.AuthenticateAsync(AuthenticationTypes.External);
            if (result == null)
            {
                Trace.Information("No external login found.");
                return new AuthenticateExternalLoginResult();
            }
            var idClaim = result.Identity.FindFirst(ClaimTypes.NameIdentifier);
            if (idClaim == null)
            {
                Trace.Error("External Authentication is missing required claim: " + ClaimTypes.NameIdentifier);
                return new AuthenticateExternalLoginResult();
            }
            
            var nameClaim = result.Identity.FindFirst(ClaimTypes.Name);
            if (nameClaim == null)
            {
                Trace.Error("External Authentication is missing required claim: " + ClaimTypes.Name);
                return new AuthenticateExternalLoginResult();
            }

            var emailClaim = result.Identity.FindFirst(ClaimTypes.Email);
            string emailSuffix = emailClaim == null ? String.Empty : (" <" + emailClaim.Value + ">");

            Authenticator auther;
            if (!Authenticators.TryGetValue(idClaim.Issuer, out auther))
            {
                auther = null;
            }

            return new AuthenticateExternalLoginResult()
            {
                Authentication = null,
                ExternalIdentity = result.Identity,
                Authenticator = auther,
                Credential = CredentialBuilder.CreateExternalCredential(idClaim.Issuer, idClaim.Value, nameClaim.Value + emailSuffix)
            };
        }

        public async virtual Task<AuthenticateExternalLoginResult> AuthenticateExternalLogin(IOwinContext context)
        {
            var result = await ReadExternalLoginCredential(context);

            // Authenticate!
            if (result.Credential != null)
            {
                result.Authentication = Authenticate(result.Credential);
            }

            return result;
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

        private async Task ReplaceCredentialInternal(User user, Credential credential)
        {
            // Find the credentials we're replacing, if any
            var toRemove = user.Credentials
                .Where(cred =>
                    // If we're replacing a password credential, remove ALL password credentials
                    (credential.Type.StartsWith(CredentialTypes.Password.Prefix, StringComparison.OrdinalIgnoreCase) &&
                     cred.Type.StartsWith(CredentialTypes.Password.Prefix, StringComparison.OrdinalIgnoreCase)) ||
                    cred.Type == credential.Type)
                .ToList();
            foreach (var cred in toRemove)
            {
                user.Credentials.Remove(cred);
                Entities.DeleteOnCommit(cred);
            }

            if (toRemove.Any())
            {
                await Auditing.SaveAuditRecord(new UserAuditRecord(
                    user, UserAuditAction.RemovedCredential, toRemove));
            }

            user.Credentials.Add(credential);

            await Auditing.SaveAuditRecord(new UserAuditRecord(
                user, UserAuditAction.AddedCredential, credential));
        }

        private static CredentialKind GetCredentialKind(string type)
        {
            if (type.StartsWith("apikey", StringComparison.OrdinalIgnoreCase))
            {
                return CredentialKind.Token;
            }
            else if (type.StartsWith("password", StringComparison.OrdinalIgnoreCase))
            {
                return CredentialKind.Password;
            }
            return CredentialKind.External;
        }

        private string FormatCredentialType(string credentialType)
        {
            string[] splitted = credentialType.Split('.');
            if (splitted.Length < 2)
            {
                return credentialType;
            }
            string prefix = splitted[0];
            string subtype = credentialType.Substring(splitted[0].Length + 1);

            Func<string, string> formatter;
            if (!_credentialFormatters.TryGetValue(prefix, out formatter))
            {
                return credentialType;
            }
            return formatter(subtype);
        }

        private string FormatExternalCredentialType(string externalType)
        {
            Authenticator auther;
            if (!Authenticators.TryGetValue(externalType, out auther))
            {
                return externalType;
            }
            var ui = auther.GetUI();
            return ui == null ? auther.Name : ui.Caption;
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

        private async Task MigrateCredentials(User user, List<Credential> creds, string password)
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
                Entities.DeleteOnCommit(cred);
            }
            await Auditing.SaveAuditRecord(new UserAuditRecord(user, UserAuditAction.RemovedCredential, toRemove));
                
            // Now add one if there are no credentials left
            if (creds.Count == 0)
            {
                var newCred = CredentialBuilder.CreatePbkdf2Password(password);
                await Auditing.SaveAuditRecord(new UserAuditRecord(user, UserAuditAction.AddedCredential, newCred));
                user.Credentials.Add(newCred);
            }

            // Save changes, if any
            Entities.SaveChanges();
        }
    }

    public class AuthenticateExternalLoginResult
    {
        public AuthenticatedUser Authentication { get; set; }
        public ClaimsIdentity ExternalIdentity { get; set; }
        public Authenticator Authenticator { get; set; }
        public Credential Credential { get; set; }
    }
}