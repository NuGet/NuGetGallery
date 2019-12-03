// Copyright (c) .NET Foundation. All rights reserved.
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
using Microsoft.Owin.Security;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication.Providers;
using NuGetGallery.Authentication.Providers.AzureActiveDirectoryV2;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGetGallery.Infrastructure.Authentication;

using static NuGetGallery.ServicesConstants;

namespace NuGetGallery.Authentication
{
    public class AuthenticationService: IAuthenticationService
    {
        private Dictionary<string, Func<string, string>> _credentialFormatters;
        private readonly IDiagnosticsSource _trace;
        private readonly IAppConfiguration _config;
        private readonly ICredentialBuilder _credentialBuilder;
        private readonly ICredentialValidator _credentialValidator;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IContentObjectService _contentObjectService;
        private readonly ITelemetryService _telemetryService;

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
            IAuditingService auditing, IEnumerable<Authenticator> providers, ICredentialBuilder credentialBuilder,
            ICredentialValidator credentialValidator, IDateTimeProvider dateTimeProvider, ITelemetryService telemetryService,
            IContentObjectService contentObjectService)
        {
            InitCredentialFormatters();

            Entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _trace = diagnostics?.SafeGetSource("AuthenticationService") ?? throw new ArgumentNullException(nameof(diagnostics));
            Auditing = auditing ?? throw new ArgumentNullException(nameof(auditing)); ;
            Authenticators = providers?.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase) ?? throw new ArgumentNullException(nameof(providers));
            _credentialBuilder = credentialBuilder ?? throw new ArgumentNullException(nameof(credentialBuilder));
            _credentialValidator = credentialValidator ?? throw new ArgumentNullException(nameof(credentialValidator));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _contentObjectService = contentObjectService ?? throw new ArgumentNullException(nameof(contentObjectService));
        }

        public IEntitiesContext Entities { get; private set; }
        public IDictionary<string, Authenticator> Authenticators { get; private set; }
        public IAuditingService Auditing { get; private set; }

        private void InitCredentialFormatters()
        {
            _credentialFormatters = new Dictionary<string, Func<string, string>>(StringComparer.OrdinalIgnoreCase) {
                { "password", _ => ServicesStrings.CredentialType_Password },
                { "apikey", _ => ServicesStrings.CredentialType_ApiKey },
                { "external", FormatExternalCredentialType }
            };
        }

        public virtual async Task<PasswordAuthenticationResult> Authenticate(string userNameOrEmail, string password)
        {
            using (_trace.Activity("Authenticate"))
            {
                var user = FindByUserNameOrEmail(userNameOrEmail);

                // Check if the user exists
                if (user == null)
                {
                    _trace.Information("No such user.");

                    await Auditing.SaveAuditRecordAsync(
                        new FailedAuthenticatedOperationAuditRecord(
                            userNameOrEmail, AuditedAuthenticatedOperationAction.FailedLoginNoSuchUser));

                    return new PasswordAuthenticationResult(PasswordAuthenticationResult.AuthenticationResult.BadCredentials);
                }

                if (user is Organization)
                {
                    _trace.Information("Cannot authenticate organization account.");

                    await Auditing.SaveAuditRecordAsync(
                        new FailedAuthenticatedOperationAuditRecord(
                            userNameOrEmail, AuditedAuthenticatedOperationAction.FailedLoginUserIsOrganization));

                    return new PasswordAuthenticationResult(PasswordAuthenticationResult.AuthenticationResult.BadCredentials);
                }

                int remainingMinutes;

                if (IsAccountLocked(user, out remainingMinutes))
                {
                    _trace.Information($"Login failed. User account is locked for the next {remainingMinutes} minutes.");

                    return new PasswordAuthenticationResult(PasswordAuthenticationResult.AuthenticationResult.AccountLocked,
                        authenticatedUser: null, lockTimeRemainingMinutes: remainingMinutes);
                }

                // Validate the password
                Credential matched;
                if (!ValidatePasswordCredential(user.Credentials, password, out matched))
                {
                    _trace.Information("Password validation failed.");

                    await UpdateFailedLoginAttempt(user);

                    await Auditing.SaveAuditRecordAsync(
                        new FailedAuthenticatedOperationAuditRecord(
                            userNameOrEmail, AuditedAuthenticatedOperationAction.FailedLoginInvalidPassword));

                    return new PasswordAuthenticationResult(PasswordAuthenticationResult.AuthenticationResult.BadCredentials);
                }

                var passwordCredentials = user
                    .Credentials
                    .Where(c => c.IsPassword())
                    .ToList();

                if (passwordCredentials.Count > 1 ||
                    !passwordCredentials.Any(c => string.Equals(c.Type, CredentialBuilder.LatestPasswordType, StringComparison.OrdinalIgnoreCase)))
                {
                    await MigrateCredentials(user, passwordCredentials, password);
                }

                // Reset failed login count upon successful login
                await UpdateSuccessfulLoginAttempt(user);

                // Return the result
                _trace.Verbose("User successfully authenticated with '" + matched.Type + "' credential");
                return new PasswordAuthenticationResult(PasswordAuthenticationResult.AuthenticationResult.Success, new AuthenticatedUser(user, matched));
            }
        }

        public virtual async Task<AuthenticatedUser> Authenticate(string apiKey)
        {
            return await AuthenticateInternal(
                FindMatchingApiKey,
                new Credential { Type = CredentialTypes.ApiKey.Prefix, Value = apiKey });
        }

        public Credential GetApiKeyCredential(string apiKey)
        {
            var credential = new Credential { Type = CredentialTypes.ApiKey.Prefix, Value = apiKey };
            return FindMatchingApiKey(credential);
        }

        public async Task RevokeApiKeyCredential(Credential apiKeyCredential, CredentialRevocationSource revocationSourceKey, bool commitChanges = true)
        {
            if (apiKeyCredential == null)
            {
                throw new ArgumentNullException(nameof(apiKeyCredential));
            }

            if (!IsActiveApiKeyCredential(apiKeyCredential))
            {
                // Revoking not active API key credential is not allowed.
                throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture,
                    ServicesStrings.RevokeCredential_UnrevocableApiKeyCredential,
                    apiKeyCredential.Key));
            }

            await Auditing.SaveAuditRecordAsync(
                new UserAuditRecord(user: apiKeyCredential.User,
                action: AuditedUserAction.RevokeCredential,
                affected: apiKeyCredential,
                revocationSource: Enum.GetName(typeof(CredentialRevocationSource), revocationSourceKey)));

            await RevokeCredential(apiKeyCredential, revocationSourceKey, commitChanges);
        }

        public bool IsActiveApiKeyCredential(Credential credential)
        {
            if (credential == null)
            {
                return false;
            }
            if (!CredentialTypes.IsApiKey(credential.Type))
            {
                return false;
            }
            if (IsCredentialExpiredOrNonScopedApiKeyNotUsedInLastDays(credential))
            {
                return false;
            }
            if (credential.RevocationSourceKey != null)
            {
                return false;
            }

            return true;
        }

        private async Task RevokeCredential(Credential credential, CredentialRevocationSource revocationSourceKey, bool commitChanges)
        {
            credential.Expires = _dateTimeProvider.UtcNow;
            credential.RevocationSourceKey = revocationSourceKey;

            if (commitChanges)
            {
                await Entities.SaveChangesAsync();
            }
        }

        public virtual async Task<AuthenticatedUser> Authenticate(Credential credential)
        {
            return await AuthenticateInternal(FindMatchingCredential, credential);
        }

        private async Task<AuthenticatedUser> AuthenticateInternal(Func<Credential, Credential> matchCredential, Credential credential)
        {
            if (credential.IsPassword())
            {
                // Password credentials cannot be used this way.
                throw new ArgumentException(ServicesStrings.PasswordCredentialsCannotBeUsedHere, nameof(credential));
            }

            using (_trace.Activity("Authenticate Credential: " + credential.Type))
            {
                var matched = matchCredential(credential);

                if (matched == null)
                {
                    _trace.Information("No user matches credential of type: " + credential.Type);

                    await Auditing.SaveAuditRecordAsync(
                        new FailedAuthenticatedOperationAuditRecord(null,
                            AuditedAuthenticatedOperationAction.FailedLoginNoSuchUser,
                            attemptedCredential: credential));

                    return null;
                }

                if (matched.User is Organization)
                {
                    _trace.Information("Cannot authenticate organization account.");

                    await Auditing.SaveAuditRecordAsync(
                        new FailedAuthenticatedOperationAuditRecord(null,
                            AuditedAuthenticatedOperationAction.FailedLoginUserIsOrganization,
                            attemptedCredential: credential));

                    return null;
                }

                if (matched.HasExpired)
                {
                    _trace.Verbose("Credential of type '" + matched.Type + "' has expired on " + matched.Expires.Value.ToString("O", CultureInfo.InvariantCulture));

                    return null;
                }

                if (matched.IsApiKey() &&
                    !matched.IsScopedApiKey() &&
                    !matched.HasBeenUsedInLastDays(_config.ExpirationInDaysForApiKeyV1))
                {
                    // API key credential was last used a long, long time ago - expire it
                    await Auditing.SaveAuditRecordAsync(
                        new UserAuditRecord(matched.User, AuditedUserAction.ExpireCredential, matched));

                    matched.Expires = _dateTimeProvider.UtcNow;
                    await Entities.SaveChangesAsync();

                    _trace.Verbose(
                        "Credential of type '" + matched.Type
                        + "' was last used on " + matched.LastUsed.Value.ToString("O", CultureInfo.InvariantCulture)
                        + " and has now expired.");

                    return null;
                }

                // update last used timestamp
                matched.LastUsed = _dateTimeProvider.UtcNow;
                await Entities.SaveChangesAsync();

                _trace.Verbose("User successfully authenticated with '" + matched.Type + "' credential");

                return new AuthenticatedUser(matched.User, matched);
            }
        }

        /// <summary>
        /// Generate the new session for the logged in user. Also, set the appropriate claims for the user in this session.
        /// The multi-factor authentication setting value can be obtained from external logins(in case of AADv2).
        /// </summary>
        /// <returns>Awaitable task</returns>
        public virtual async Task CreateSessionAsync(IOwinContext owinContext, AuthenticatedUser authenticatedUser, bool wasMultiFactorAuthenticated = false)
        {
            // Create a claims identity for the session
            ClaimsIdentity identity = CreateIdentity(authenticatedUser.User, AuthenticationTypes.LocalUser, await GetUserLoginClaims(authenticatedUser, wasMultiFactorAuthenticated));

            // Issue the session token and clean up the external token if present
            owinContext.Authentication.SignIn(new AuthenticationProperties() { IsPersistent = true }, identity);
            owinContext.Authentication.SignOut(AuthenticationTypes.External);

            _telemetryService.TrackUserLogin(authenticatedUser.User, authenticatedUser.CredentialUsed, wasMultiFactorAuthenticated);

            // Write an audit record
            await Auditing.SaveAuditRecordAsync(
                new UserAuditRecord(authenticatedUser.User, AuditedUserAction.Login, authenticatedUser.CredentialUsed));
        }

        private async Task<Claim[]> GetUserLoginClaims(AuthenticatedUser user, bool wasMultiFactorAuthenticated)
        {
            await _contentObjectService.Refresh();

            var claims = new List<Claim>();

            if (_contentObjectService.LoginDiscontinuationConfiguration.IsLoginDiscontinued(user))
            {
                ClaimsExtensions.AddBooleanClaim(claims, NuGetClaims.DiscontinuedLogin);
            }

            if (user.User.HasPasswordCredential())
            {
                ClaimsExtensions.AddBooleanClaim(claims, NuGetClaims.PasswordLogin);
            }

            if (user.User.HasExternalCredential())
            {
                ClaimsExtensions.AddBooleanClaim(claims, NuGetClaims.ExternalLogin);

                var externalIdentities = user.User
                    .Credentials
                    .Where(cred => cred.IsExternal())
                    .Select(cred => cred.Identity)
                    .ToArray();
                
                var identityList = string.Join(" or ", externalIdentities);
                ClaimsExtensions.AddExternalCredentialIdentityClaim(claims, identityList);
            }

            if (user.User.EnableMultiFactorAuthentication)
            {
                ClaimsExtensions.AddBooleanClaim(claims, NuGetClaims.EnabledMultiFactorAuthentication);
            }

            if (wasMultiFactorAuthenticated)
            {
                ClaimsExtensions.AddBooleanClaim(claims, NuGetClaims.WasMultiFactorAuthenticated);
            }

            ClaimsExtensions.AddExternalLoginCredentialTypeClaim(claims, user.CredentialUsed.Type);

            return claims.ToArray();
        }

        public virtual async Task<AuthenticatedUser> Register(string username, string emailAddress, Credential credential, bool autoConfirm = false)
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
                    throw new EntityException(ServicesStrings.UsernameNotAvailable, username);
                }
                else
                {
                    throw new EntityException(ServicesStrings.EmailAddressBeingUsed, emailAddress);
                }
            }

            var newUser = new User(username)
            {
                EmailAllowed = true,
                UnconfirmedEmailAddress = emailAddress,
                EmailConfirmationToken = CryptographyService.GenerateToken(),
                NotifyPackagePushed = true,
                CreatedUtc = _dateTimeProvider.UtcNow
            };

            // Add a credential for the password
            newUser.Credentials.Add(credential);

            if (!_config.ConfirmEmailAddresses || autoConfirm)
            {
                newUser.ConfirmEmailAddress();
            }

            // Write an audit record
            await Auditing.SaveAuditRecordAsync(new UserAuditRecord(newUser, AuditedUserAction.Register, credential));

            Entities.Users.Add(newUser);
            await Entities.SaveChangesAsync();

            _telemetryService.TrackNewUserRegistrationEvent(newUser, credential);
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
                throw new InvalidOperationException(ServicesStrings.UserNotFound);
            }

            return ReplaceCredential(user, credential);
        }

        public virtual async Task<bool> TryReplaceCredential(User user, Credential credential)
        {
            if (user == null || credential == null)
            {
                return false;
            }

            // Check user credentials for existing cred for optimization to avoid expensive DB query
            if (!user.HasCredential(credential) && FindMatchingCredential(credential) != null)
            {
                // Existing credential for a registered account
                return false;
            }

            try
            {
                await ReplaceCredential(user, credential);
                return true;
            }
            catch (InvalidOperationException)
            {
                // ReplaceCredential could throw InvalidOperationException if the user is an Organization.
                // We shouldn't get into this situation ideally. Just being thorough.
                return false;
            }
        }

        public virtual async Task ReplaceCredential(User user, Credential credential)
        {
            await ReplaceCredentialInternal(user, credential);
            await Entities.SaveChangesAsync();
        }

        public virtual async Task<Credential> ResetPasswordWithToken(string username, string token, string newPassword)
        {
            if (string.IsNullOrEmpty(newPassword))
            {
                throw new ArgumentNullException(nameof(newPassword));
            }

            var user = Entities
                .Users
                .Include(u => u.Credentials)
                .SingleOrDefault(u => u.Username == username);

            if (user != null && string.Equals(user.PasswordResetToken, token, StringComparison.Ordinal) && !user.PasswordResetTokenExpirationDate.IsInThePast())
            {
                if (!user.Confirmed)
                {
                    throw new InvalidOperationException(ServicesStrings.UserIsNotYetConfirmed);
                }

                var cred = _credentialBuilder.CreatePasswordCredential(newPassword);
                await ReplaceCredentialInternal(user, cred);
                user.PasswordResetToken = null;
                user.PasswordResetTokenExpirationDate = null;
                user.FailedLoginCount = 0;
                user.LastFailedLoginUtc = null;
                await Entities.SaveChangesAsync();
                return cred;
            }

            return null;
        }

        public virtual async Task<PasswordResetResult> GeneratePasswordResetToken(string usernameOrEmail, int expirationInMinutes)
        {
            if (String.IsNullOrEmpty(usernameOrEmail))
            {
                throw new ArgumentNullException(nameof(usernameOrEmail));
            }
            if (expirationInMinutes < 1)
            {
                throw new ArgumentException(
                    ServicesStrings.TokenExpirationShouldGiveUser1MinuteToChangePassword, nameof(expirationInMinutes));
            }
            var user = FindByUserNameOrEmail(usernameOrEmail);
            if (user == null)
            {
                return new PasswordResetResult(PasswordResetResultType.UserNotFound, user: null);
            }
            var resultType = await GeneratePasswordResetToken(user, expirationInMinutes);
            return new PasswordResetResult(resultType, user);
        }

        public virtual async Task<PasswordResetResultType> GeneratePasswordResetToken(User user, int expirationInMinutes)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            if (expirationInMinutes < 1)
            {
                throw new ArgumentException(
                    ServicesStrings.TokenExpirationShouldGiveUser1MinuteToChangePassword, nameof(expirationInMinutes));
            }

            if (!user.Confirmed)
            {
                return PasswordResetResultType.UserNotConfirmed;
            }

            if (!string.IsNullOrEmpty(user.PasswordResetToken) && !user.PasswordResetTokenExpirationDate.IsInThePast())
            {
                return PasswordResetResultType.Success;
            }

            user.PasswordResetToken = CryptographyService.GenerateToken();
            user.PasswordResetTokenExpirationDate = _dateTimeProvider.UtcNow.AddMinutes(expirationInMinutes);

            var passwordCredential = user.Credentials.FirstOrDefault(
                credential => credential.Type.StartsWith(CredentialTypes.Password.Prefix, StringComparison.OrdinalIgnoreCase));

            UserAuditRecord auditRecord;

            if (passwordCredential == null)
            {
                auditRecord = new UserAuditRecord(user, AuditedUserAction.RequestPasswordReset);
            }
            else
            {
                auditRecord = new UserAuditRecord(user, AuditedUserAction.RequestPasswordReset, passwordCredential);
            }

            await Auditing.SaveAuditRecordAsync(auditRecord);

            await Entities.SaveChangesAsync();

            return PasswordResetResultType.Success;
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
            var passwordCredential = _credentialBuilder.CreatePasswordCredential(newPassword);
            await ReplaceCredentialInternal(user, passwordCredential);

            // Save changes
            await Entities.SaveChangesAsync();
            return true;
        }

        public virtual ActionResult Challenge(string providerName, string redirectUrl, AuthenticationPolicy policy = null)
        {
            Authenticator provider;

            if (!Authenticators.TryGetValue(providerName, out provider))
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture,
                    ServicesStrings.UnknownAuthenticationProvider,
                    providerName));
            }

            if (!provider.BaseConfig.Enabled)
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture,
                    ServicesStrings.AuthenticationProviderDisabled,
                    providerName));
            }

            return provider.Challenge(redirectUrl, policy);
        }

        public virtual async Task AddCredential(User user, Credential credential)
        {
            if (user is Organization)
            {
                throw new InvalidOperationException(ServicesStrings.OrganizationsCannotCreateCredentials);
            }

            await Auditing.SaveAuditRecordAsync(new UserAuditRecord(user, AuditedUserAction.AddCredential, credential));
            user.Credentials.Add(credential);
            await Entities.SaveChangesAsync();

            _telemetryService.TrackNewCredentialCreated(user, credential);
        }

        public virtual CredentialViewModel DescribeCredential(Credential credential)
        {
            var kind = GetCredentialKind(credential.Type);
            Authenticator authenticator = null;

            if (kind == CredentialKind.External)
            {
                if (string.IsNullOrEmpty(credential.TenantId))
                {
                    string providerName = credential.Type.Split('.')[1];
                    Authenticators.TryGetValue(providerName, out authenticator);
                }
                else
                {
                    authenticator = Authenticators
                        .Values
                        .FirstOrDefault(provider => provider.Name.Equals(AzureActiveDirectoryV2Authenticator.DefaultAuthenticationType, StringComparison.OrdinalIgnoreCase));
                }
            }

            var credentialViewModel = new CredentialViewModel
            {
                Key = credential.Key,
                Type = credential.Type,
                TypeCaption = FormatCredentialType(credential.Type),
                Identity = credential.Identity,
                Created = credential.Created,
                Expires = credential.Expires,
                Kind = kind,
                AuthUI = authenticator?.GetUI(),
                Description = credential.Description,
                Scopes = credential.Scopes.Select(s => new ScopeViewModel(
                        s.Owner?.Username ?? credential.User.Username,
                        s.Subject,
                        NuGetScopes.Describe(s.AllowedAction)))
                    .ToList(),
                ExpirationDuration = credential.ExpirationTicks != null ? new TimeSpan?(new TimeSpan(credential.ExpirationTicks.Value)) : null,
                RevocationSource = credential.RevocationSourceKey != null ? Enum.GetName(typeof(CredentialRevocationSource), credential.RevocationSourceKey) : null,
            };

            credentialViewModel.HasExpired = IsCredentialExpiredOrNonScopedApiKeyNotUsedInLastDays(credential);

            credentialViewModel.Description = credentialViewModel.IsNonScopedApiKey
                ? ServicesStrings.NonScopedApiKeyDescription : credentialViewModel.Description;

            return credentialViewModel;
        }

        private bool IsCredentialExpiredOrNonScopedApiKeyNotUsedInLastDays(Credential credential)
        {
            return credential.HasExpired || (credential.IsApiKey() && !credential.IsScopedApiKey()
                && !credential.HasBeenUsedInLastDays(_config.ExpirationInDaysForApiKeyV1));
        }

        public virtual async Task RemoveCredential(User user, Credential cred, bool commitChanges = true)
        {
            await Auditing.SaveAuditRecordAsync(new UserAuditRecord(user, AuditedUserAction.RemoveCredential, cred));
            user.Credentials.Remove(cred);
            Entities.Credentials.Remove(cred);

            if (commitChanges)
            {
                await Entities.SaveChangesAsync();
            }
        }

        public virtual async Task EditCredentialScopes(User user, Credential cred, ICollection<Scope> newScopes)
        {
            foreach (var oldScope in cred.Scopes.ToArray())
            {
                Entities.Scopes.Remove(oldScope);
            }

            cred.Scopes = newScopes;

            await Entities.SaveChangesAsync();

            await Auditing.SaveAuditRecordAsync(new UserAuditRecord(user, AuditedUserAction.EditCredential, cred));
        }

        public virtual async Task<AuthenticateExternalLoginResult> ReadExternalLoginCredential(IOwinContext context)
        {
            var result = await context.Authentication.AuthenticateAsync(AuthenticationTypes.External);
            if (result?.Identity?.Claims == null)
            {
                _trace.Information("No external login found.");
                return new AuthenticateExternalLoginResult();
            }

            var externalIdentity = result.Identity;
            Authenticator authenticator = Authenticators
                .Values
                .FirstOrDefault(a => a.IsProviderForIdentity(externalIdentity));

            if (authenticator == null)
            {
                _trace.Error($"No authenticator found for identity: {externalIdentity.AuthenticationType}");
                return new AuthenticateExternalLoginResult();
            }

            try
            {
                var userInfo = authenticator.GetIdentityInformation(externalIdentity);
                var emailSuffix = userInfo.Email == null ? string.Empty : (" <" + userInfo.Email + ">");
                var identity = userInfo.Name + emailSuffix;
                return new AuthenticateExternalLoginResult()
                {
                    Authentication = null,
                    ExternalIdentity = externalIdentity,
                    Authenticator = authenticator,
                    Credential = _credentialBuilder.CreateExternalCredential(userInfo.AuthenticationType, userInfo.Identifier, identity, userInfo.TenantId),
                    LoginDetails = new ExternalLoginSessionDetails(userInfo.Email, userInfo.UsedMultiFactorAuthentication),
                    UserInfo = userInfo
                };
            }
            catch (Exception ex)
            {
                _trace.Error(ex.Message);
                return new AuthenticateExternalLoginResult();
            }
        }

        public virtual async Task<AuthenticateExternalLoginResult> AuthenticateExternalLogin(IOwinContext context)
        {
            var result = await ReadExternalLoginCredential(context);

            // Authenticate!
            if (result.Credential != null)
            {
                // We want to audit the received credential from external authentication. We use the UserAuditRecord
                // for easier logging, since it actually needs a `User`, instead we use a dummy user because at this point
                // we do not have the actual user context since this request has not yet been authenticated.
                await Auditing.SaveAuditRecordAsync(new UserAuditRecord(
                    new User("NonExistentDummyAuditUser"), AuditedUserAction.ExternalLoginAttempt, result.Credential));

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
            if (user is Organization)
            {
                throw new InvalidOperationException(ServicesStrings.OrganizationsCannotCreateCredentials);
            }

            string replaceCredPrefix = null;
            if (credential.IsPassword())
            {
                replaceCredPrefix = CredentialTypes.Password.Prefix;
            }
            else if (credential.IsExternal())
            {
                replaceCredPrefix = CredentialTypes.External.Prefix;
            }

            Func<Credential, bool> replacingPredicate;
            if (!string.IsNullOrEmpty(replaceCredPrefix))
            {
                 replacingPredicate = cred => cred.Type.StartsWith(replaceCredPrefix, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                replacingPredicate = cred => cred.Type.Equals(credential.Type, StringComparison.OrdinalIgnoreCase);
            }

            var toRemove = user.Credentials
                .Where(replacingPredicate) 
                .ToList();

            foreach (var cred in toRemove)
            {
                user.Credentials.Remove(cred);
                Entities.DeleteOnCommit(cred);
            }

            if (toRemove.Any())
            {
                await Auditing.SaveAuditRecordAsync(new UserAuditRecord(
                    user, AuditedUserAction.RemoveCredential, toRemove));
            }

            user.Credentials.Add(credential);

            await Auditing.SaveAuditRecordAsync(new UserAuditRecord(
                user, AuditedUserAction.AddCredential, credential));
        }

        private static CredentialKind GetCredentialKind(string type)
        {
            if (CredentialTypes.IsApiKey(type))
            {
                return CredentialKind.Token;
            }
            else if (CredentialTypes.IsPassword(type))
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
            Authenticator authenticator;
            if (!Authenticators.TryGetValue(externalType, out authenticator))
            {
                return externalType;
            }
            var ui = authenticator.GetUI();
            return ui == null ? authenticator.Name : ui.AccountNoun;
        }

        private Credential FindMatchingCredential(Credential credential)
        {
            var results = Entities
                .Set<Credential>()
                .Include(u => u.User)
                .Include(u => u.User.Roles)
                .Include(u => u.Scopes)
                .Where(c => c.Type == credential.Type && c.Value == credential.Value && c.TenantId == credential.TenantId)
                .ToList();

            return ValidateFoundCredentials(results, credential.Type);
        }

        private Credential FindMatchingApiKey(Credential apiKeyCredential)
        {
            var allCredentials = Entities
                .Set<Credential>()
                .Include(u => u.User)
                .Include(u => u.User.Roles)
                .Include(u => u.Scopes);

            var results = _credentialValidator.GetValidCredentialsForApiKey(allCredentials, apiKeyCredential.Value);

            return ValidateFoundCredentials(results, ServicesStrings.CredentialType_ApiKey);
        }

        private Credential ValidateFoundCredentials(IList<Credential> results, string credentialType)
        {
            if (results.Count > 1)
            {
                // Don't put the credential itself in trace, but do put the key for lookup later.
                string message = string.Format(
                    CultureInfo.CurrentCulture,
                    ServicesStrings.MultipleMatchingCredentials,
                    credentialType,
                    results.First().Key);
                _trace.Error(message);
                throw new InvalidOperationException(message);
            }

            return results.FirstOrDefault();
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
                    _trace.Warning($"Multiple user accounts with a single email address were found. Count: {allMatches.Count}");
                }
            }
            return user;
        }

        private async Task UpdateFailedLoginAttempt(User user)
        {
            user.FailedLoginCount += 1;
            user.LastFailedLoginUtc = _dateTimeProvider.UtcNow;

            await Entities.SaveChangesAsync();
        }

        private async Task UpdateSuccessfulLoginAttempt(User user)
        {
            user.FailedLoginCount = 0;
            user.LastFailedLoginUtc = null;

            await Entities.SaveChangesAsync();
        }

        private bool IsAccountLocked(User user, out int remainingMinutes)
        {
            if (user.FailedLoginCount > 0)
            {
                var currentTime = _dateTimeProvider.UtcNow;
                var unlockTime = CalculateAccountUnlockTime(user.FailedLoginCount, user.LastFailedLoginUtc.Value);

                if (unlockTime > currentTime)
                {
                    remainingMinutes = (int)Math.Ceiling((unlockTime - currentTime).TotalMinutes);
                    return true;
                }
            }

            remainingMinutes = 0;
            return false;
        }

        private DateTime CalculateAccountUnlockTime(int failedLoginCount, DateTime lastFailedLogin)
        {
            int lockoutPeriodInMinutes = (int)Math.Pow(AccountLockoutMultiplierInMinutes, (int)((double)failedLoginCount / AllowedLoginAttempts) - 1);

            return lastFailedLogin + TimeSpan.FromMinutes(lockoutPeriodInMinutes);
        }

        public virtual bool ValidatePasswordCredential(IEnumerable<Credential> creds, string password, out Credential matched)
        {
            matched = creds.FirstOrDefault(c => _credentialValidator.ValidatePasswordCredential(c, password));
            return matched != null;
        }

        private async Task MigrateCredentials(User user, List<Credential> creds, string password)
        {
            // Authenticate already validated that user is not an Organization, so no need to replicate here.

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
            await Auditing.SaveAuditRecordAsync(new UserAuditRecord(user, AuditedUserAction.RemoveCredential, toRemove));

            // Now add one if there are no credentials left
            if (creds.Count == 0)
            {
                var newCred = _credentialBuilder.CreatePasswordCredential(password);
                await Auditing.SaveAuditRecordAsync(new UserAuditRecord(user, AuditedUserAction.AddCredential, newCred));
                user.Credentials.Add(newCred);
            }

            // Save changes, if any
            await Entities.SaveChangesAsync();
        }
    }
}