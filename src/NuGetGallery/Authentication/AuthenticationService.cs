﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Mvc;
using Microsoft.Owin;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication.Providers;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGetGallery.Infrastructure.Authentication;

namespace NuGetGallery.Authentication
{
    public class AuthenticationService
    {
        private Dictionary<string, Func<string, string>> _credentialFormatters;
        private readonly IDiagnosticsSource _trace;
        private readonly IAppConfiguration _config;
        private readonly ICredentialBuilder _credentialBuilder;
        private readonly ICredentialValidator _credentialValidator;

        /// <summary>
        /// This ctor is used for test only.
        /// </summary>
        protected AuthenticationService()
        {
            Auditing = AuditingService.None;
            Authenticators = new Dictionary<string, Authenticator>();
            InitCredentialFormatters();
        }

        public AuthenticationService(
            IEntitiesContext entities, IAppConfiguration config, IDiagnosticsService diagnostics,
            AuditingService auditing, IEnumerable<Authenticator> providers, ICredentialBuilder credentialBuilder,
            ICredentialValidator credentialValidator)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            if (auditing == null)
            {
                throw new ArgumentNullException(nameof(auditing));
            }

            if (providers == null)
            {
                throw new ArgumentNullException(nameof(providers));
            }

            if (credentialBuilder == null)
            {
                throw new ArgumentNullException(nameof(credentialBuilder));
            }

            if (credentialValidator == null)
            {
                throw new ArgumentNullException(nameof(credentialValidator));
            }

            InitCredentialFormatters();

            Entities = entities;
            _config = config;
            Auditing = auditing;
            _trace = diagnostics.SafeGetSource("AuthenticationService");
            Authenticators = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
            _credentialBuilder = credentialBuilder;
            _credentialValidator = credentialValidator;
        }

        public IEntitiesContext Entities { get; private set; }
        public IDictionary<string, Authenticator> Authenticators { get; private set; }
        public AuditingService Auditing { get; private set; }

        private void InitCredentialFormatters()
        {
            _credentialFormatters = new Dictionary<string, Func<string, string>>(StringComparer.OrdinalIgnoreCase) {
                { "password", _ => Strings.CredentialType_Password },
                { "apikey", _ => Strings.CredentialType_ApiKey },
                { "external", FormatExternalCredentialType }
            };
        }

        public virtual async Task<AuthenticatedUser> Authenticate(string userNameOrEmail, string password)
        {
            using (_trace.Activity("Authenticate:" + userNameOrEmail))
            {
                var user = FindByUserNameOrEmail(userNameOrEmail);

                // Check if the user exists
                if (user == null)
                {
                    _trace.Information("No such user: " + userNameOrEmail);
                    
                    await Auditing.SaveAuditRecord(
                        new FailedAuthenticatedOperationAuditRecord(
                            userNameOrEmail, AuditedAuthenticatedOperationAction.FailedLoginNoSuchUser));

                    return null;
                }

                // Validate the password
                Credential matched;
                if (!ValidatePasswordCredential(user.Credentials, password, out matched))
                {
                    _trace.Information("Password validation failed: " + userNameOrEmail);

                    await Auditing.SaveAuditRecord(
                        new FailedAuthenticatedOperationAuditRecord(
                            userNameOrEmail, AuditedAuthenticatedOperationAction.FailedLoginInvalidPassword));

                    return null;
                }

                var passwordCredentials = user
                    .Credentials
                    .Where(c => CredentialTypes.IsPassword(c.Type))
                    .ToList();

                if (passwordCredentials.Count > 1 ||
                    !passwordCredentials.Any(c => string.Equals(c.Type, CredentialBuilder.LatestPasswordType, StringComparison.OrdinalIgnoreCase)))
                {
                    await MigrateCredentials(user, passwordCredentials, password);
                }

                // Return the result
                _trace.Verbose("Successfully authenticated '" + user.Username + "' with '" + matched.Type + "' credential");
                return new AuthenticatedUser(user, matched);
            }
        }

        public virtual async Task<AuthenticatedUser> Authenticate(Credential credential)
        {
            if (credential.Type.StartsWith(CredentialTypes.Password.Prefix, StringComparison.OrdinalIgnoreCase))
            {
                // Password credentials cannot be used this way.
                throw new ArgumentException(Strings.PasswordCredentialsCannotBeUsedHere, nameof(credential));
            }

            using (_trace.Activity("Authenticate Credential: " + credential.Type))
            {
                var matched = FindMatchingCredential(credential);

                if (matched == null)
                {
                    _trace.Information("No user matches credential of type: " + credential.Type);

                    await Auditing.SaveAuditRecord(
                        new FailedAuthenticatedOperationAuditRecord(null, AuditedAuthenticatedOperationAction.FailedLoginNoSuchUser, attemptedCredential: credential));

                    return null;
                }
                
                if (matched.HasExpired)
                {
                    _trace.Verbose("Credential of type '" + matched.Type + "' for user '" + matched.User.Username + "' has expired on " + matched.Expires.Value.ToString("O", CultureInfo.InvariantCulture));

                    return null;
                }

                if (matched.Type == CredentialTypes.ApiKeyV1 
                    && !matched.HasBeenUsedInLastDays(_config.ExpirationInDaysForApiKeyV1))
                {
                    // API key credential was last used a long, long time ago - expire it
                    await Auditing.SaveAuditRecord(
                        new UserAuditRecord(matched.User, AuditedUserAction.ExpireCredential, matched));

                    matched.Expires = DateTime.UtcNow;
                    await Entities.SaveChangesAsync();

                    _trace.Verbose(
                        "Credential of type '" + matched.Type 
                        + "' for user '" + matched.User.Username 
                        + "' was last used on " + matched.LastUsed.Value.ToString("O", CultureInfo.InvariantCulture)
                        + " and has now expired.");

                    return null;
                }

                // update last used timestamp
                matched.LastUsed = DateTime.UtcNow;
                await Entities.SaveChangesAsync();

                _trace.Verbose("Successfully authenticated '" + matched.User.Username + "' with '" + matched.Type + "' credential");

                return new AuthenticatedUser(matched.User, matched);
            }
        }

        public virtual async Task CreateSessionAsync(IOwinContext owinContext, AuthenticatedUser user)
        {
            // Create a claims identity for the session
            ClaimsIdentity identity = CreateIdentity(user.User, AuthenticationTypes.LocalUser);

            // Issue the session token and clean up the external token if present
            owinContext.Authentication.SignIn(identity);
            owinContext.Authentication.SignOut(AuthenticationTypes.External);
            
            // Write an audit record
            await Auditing.SaveAuditRecord(
                new UserAuditRecord(user.User, AuditedUserAction.Login, user.CredentialUsed));
        }

        public virtual async Task<AuthenticatedUser> Register(string username, string emailAddress, Credential credential)
        {
            if (_config.FeedOnlyMode)
            {
                throw new FeedOnlyModeException(FeedOnlyModeException.FeedOnlyModeError);
            }

            var existingUser = Entities.Users
                .FirstOrDefault(u => u.Username == username || u.EmailAddress == emailAddress);
            if (existingUser != null)
            {
                if (string.Equals(existingUser.Username, username, StringComparison.OrdinalIgnoreCase))
                {
                    throw new EntityException(Strings.UsernameNotAvailable, username);
                }
                else
                {
                    throw new EntityException(Strings.EmailAddressBeingUsed, emailAddress);
                }
            }

            var newUser = new User(username)
            {
                EmailAllowed = true,
                UnconfirmedEmailAddress = emailAddress,
                EmailConfirmationToken = CryptographyService.GenerateToken(),
                NotifyPackagePushed = true,
                CreatedUtc = DateTime.UtcNow
            };

            // Add a credential for the password and the API Key
            newUser.Credentials.Add(_credentialBuilder.CreateApiKey(TimeSpan.FromDays(_config.ExpirationInDaysForApiKeyV1)));
            newUser.Credentials.Add(credential);

            if (!_config.ConfirmEmailAddresses)
            {
                newUser.ConfirmEmailAddress();
            }

            // Write an audit record
            await Auditing.SaveAuditRecord(new UserAuditRecord(newUser, AuditedUserAction.Register));

            Entities.Users.Add(newUser);
            await Entities.SaveChangesAsync();

            return new AuthenticatedUser(newUser, credential);
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
            await Entities.SaveChangesAsync();
        }

        public virtual async Task<Credential> ResetPasswordWithToken(string username, string token, string newPassword)
        {
            if (String.IsNullOrEmpty(newPassword))
            {
                throw new ArgumentNullException(nameof(newPassword));
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

                var cred = _credentialBuilder.CreatePasswordCredential(newPassword);
                await ReplaceCredentialInternal(user, cred);
                user.PasswordResetToken = null;
                user.PasswordResetTokenExpirationDate = null;
                await Entities.SaveChangesAsync();
                return cred;
            }

            return null;
        }

        public virtual async Task<User> GeneratePasswordResetToken(string usernameOrEmail, int expirationInMinutes)
        {
            if (String.IsNullOrEmpty(usernameOrEmail))
            {
                throw new ArgumentNullException(nameof(usernameOrEmail));
            }
            if (expirationInMinutes < 1)
            {
                throw new ArgumentException(
                    Strings.TokenExpirationShouldGiveUser1MinuteToChangePassword, nameof(expirationInMinutes));
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
                throw new ArgumentNullException(nameof(user));
            }
            if (expirationInMinutes < 1)
            {
                throw new ArgumentException(
                    Strings.TokenExpirationShouldGiveUser1MinuteToChangePassword, nameof(expirationInMinutes));
            }

            if (!user.Confirmed)
            {
                throw new InvalidOperationException(Strings.UserIsNotYetConfirmed);
            }

            if (!string.IsNullOrEmpty(user.PasswordResetToken) && !user.PasswordResetTokenExpirationDate.IsInThePast())
            {
                return;
            }

            user.PasswordResetToken = CryptographyService.GenerateToken();
            user.PasswordResetTokenExpirationDate = DateTime.UtcNow.AddMinutes(expirationInMinutes);

            await Auditing.SaveAuditRecord(new UserAuditRecord(user, AuditedUserAction.RequestPasswordReset));

            await Entities.SaveChangesAsync();
        }

        public virtual async Task<bool> ChangePassword(User user, string oldPassword, string newPassword, bool resetApiKey)
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
            var passwordCredential = _credentialBuilder.CreatePasswordCredential(newPassword);
            await ReplaceCredentialInternal(user, passwordCredential);

            // Reset existing API keys
            if (resetApiKey)
            {
                var apiKeyCredential = _credentialBuilder.CreateApiKey(TimeSpan.FromDays(_config.ExpirationInDaysForApiKeyV1));

                await ReplaceCredentialInternal(user, apiKeyCredential);
            }

            // Save changes
            await Entities.SaveChangesAsync();
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
            await Auditing.SaveAuditRecord(new UserAuditRecord(user, AuditedUserAction.AddCredential, credential));
            user.Credentials.Add(credential);
            await Entities.SaveChangesAsync();
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

            return new CredentialViewModel
            {
                Type = credential.Type,
                TypeCaption = FormatCredentialType(credential.Type),
                Identity = credential.Identity,
                Value = kind == CredentialKind.Token ? credential.Value : String.Empty,
                Created = credential.Created,
                Expires = credential.Expires,
                LastUsed = credential.LastUsed,
                Kind = kind,
                AuthUI = auther?.GetUI()
            };
        }

        public virtual async Task RemoveCredential(User user, Credential cred)
        {
            await Auditing.SaveAuditRecord(new UserAuditRecord(user, AuditedUserAction.RemoveCredential, cred));
            user.Credentials.Remove(cred);
            Entities.Credentials.Remove(cred);
            await Entities.SaveChangesAsync();
        }

        public virtual async Task<AuthenticateExternalLoginResult> ReadExternalLoginCredential(IOwinContext context)
        {
            var result = await context.Authentication.AuthenticateAsync(AuthenticationTypes.External);
            if (result == null)
            {
                _trace.Information("No external login found.");
                return new AuthenticateExternalLoginResult();
            }
            var idClaim = result.Identity.FindFirst(ClaimTypes.NameIdentifier);
            if (idClaim == null)
            {
                _trace.Error("External Authentication is missing required claim: " + ClaimTypes.NameIdentifier);
                return new AuthenticateExternalLoginResult();
            }

            var nameClaim = result.Identity.FindFirst(ClaimTypes.Name);
            if (nameClaim == null)
            {
                _trace.Error("External Authentication is missing required claim: " + ClaimTypes.Name);
                return new AuthenticateExternalLoginResult();
            }

            var emailClaim = result.Identity.FindFirst(ClaimTypes.Email);
            string emailSuffix = emailClaim == null ? String.Empty : (" <" + emailClaim.Value + ">");

            Authenticator auther;
            string authenticationType = idClaim.Issuer;
            if (!Authenticators.TryGetValue(idClaim.Issuer, out auther))
            {
                foreach (var authenticator in Authenticators.Values)
                {
                    if (authenticator.TryMapIssuerToAuthenticationType(idClaim.Issuer, out authenticationType))
                    {
                        auther = authenticator;
                        break;
                    }
                }
            }

            return new AuthenticateExternalLoginResult()
            {
                Authentication = null,
                ExternalIdentity = result.Identity,
                Authenticator = auther,
                Credential = _credentialBuilder.CreateExternalCredential(authenticationType, idClaim.Value, nameClaim.Value + emailSuffix)
            };
        }

        public virtual async Task<AuthenticateExternalLoginResult> AuthenticateExternalLogin(IOwinContext context)
        {
            var result = await ReadExternalLoginCredential(context);

            // Authenticate!
            if (result.Credential != null)
            {
                result.Authentication = await Authenticate(result.Credential);
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
                    user, AuditedUserAction.RemoveCredential, toRemove));
            }

            user.Credentials.Add(credential);

            await Auditing.SaveAuditRecord(new UserAuditRecord(
                user, AuditedUserAction.AddCredential, credential));
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
                string message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.MultipleMatchingCredentials,
                    credential.Type,
                    results.First().Key);
                _trace.Error(message);
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
                    _trace.Warning("Multiple user accounts with email address: " + userNameOrEmail + " found: " + String.Join(", ", allMatches.Select(u => u.Username)));
                }
            }
            return user;
        }

        public bool ValidatePasswordCredential(IEnumerable<Credential> creds, string password, out Credential matched)
        {
            matched = creds.FirstOrDefault(c => _credentialValidator.ValidatePasswordCredential(c, password));
            return matched != null;
        }

        private async Task MigrateCredentials(User user, List<Credential> creds, string password)
        {
            var toRemove = creds.Where(c =>
                !string.Equals(
                    c.Type,
                    CredentialBuilder.LatestPasswordType,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Remove any non latest credentials
            foreach (var cred in toRemove)
            {
                creds.Remove(cred);
                user.Credentials.Remove(cred);
                Entities.DeleteOnCommit(cred);
            }
            await Auditing.SaveAuditRecord(new UserAuditRecord(user, AuditedUserAction.RemoveCredential, toRemove));

            // Now add one if there are no credentials left
            if (creds.Count == 0)
            {
                var newCred = _credentialBuilder.CreatePasswordCredential(password);
                await Auditing.SaveAuditRecord(new UserAuditRecord(user, AuditedUserAction.AddCredential, newCred));
                user.Credentials.Add(newCred);
            }

            // Save changes, if any
            await Entities.SaveChangesAsync();
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