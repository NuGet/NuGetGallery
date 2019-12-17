// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGetGallery.Infrastructure.Authentication;
using NuGetGallery.Security;
using Crypto = NuGetGallery.CryptographyService;

namespace NuGetGallery
{
    public class UserService : IUserService
    {
        public IAppConfiguration Config { get; protected set; }

        public IEntityRepository<User> UserRepository { get; protected set; }

        public IEntityRepository<Role> RoleRepository { get; protected set; }

        public IEntityRepository<Credential> CredentialRepository { get; protected set; }

        public IEntityRepository<Organization> OrganizationRepository { get; protected set; }

        public IAuditingService Auditing { get; protected set; }

        public IEntitiesContext EntitiesContext { get; protected set; }

        public IContentObjectService ContentObjectService { get; protected set; }

        public ISecurityPolicyService SecurityPolicyService { get; set; }

        public IDateTimeProvider DateTimeProvider { get; protected set; }

        public ITelemetryService TelemetryService { get; protected set; }

        public IDiagnosticsSource DiagnosticsSource { get; protected set; }

        protected UserService() { }

        public UserService(
            IAppConfiguration config,
            IEntityRepository<User> userRepository,
            IEntityRepository<Role> roleRepository,
            IEntityRepository<Credential> credentialRepository,
            IEntityRepository<Organization> organizationRepository,
            IAuditingService auditing,
            IEntitiesContext entitiesContext,
            IContentObjectService contentObjectService,
            ISecurityPolicyService securityPolicyService,
            IDateTimeProvider dateTimeProvider,
            ICredentialBuilder credentialBuilder,
            ITelemetryService telemetryService,
            IDiagnosticsService diagnosticsService)
            : this()
        {
            Config = config;
            UserRepository = userRepository;
            RoleRepository = roleRepository;
            CredentialRepository = credentialRepository;
            OrganizationRepository = organizationRepository;
            Auditing = auditing;
            EntitiesContext = entitiesContext;
            ContentObjectService = contentObjectService;
            SecurityPolicyService = securityPolicyService;
            DateTimeProvider = dateTimeProvider;
            TelemetryService = telemetryService;
            DiagnosticsSource = diagnosticsService.SafeGetSource(nameof(UserService));
        }

        public async Task<MembershipRequest> AddMembershipRequestAsync(Organization organization, string memberName, bool isAdmin)
        {
            organization = organization ?? throw new ArgumentNullException(nameof(organization));

            var membership = FindMembershipByUsername(organization, memberName);
            if (membership != null)
            {
                throw new EntityException(string.Format(CultureInfo.CurrentCulture,
                    ServicesStrings.AddMember_AlreadyAMember, memberName));
            }

            var request = FindMembershipRequestByUsername(organization, memberName);
            if (request != null)
            {
                // If there is already an existing request, return it.
                // If the existing request grants collaborator but we are trying to create a request that grants admin, update the request to grant admin.
                request.IsAdmin = isAdmin || request.IsAdmin;
                await EntitiesContext.SaveChangesAsync();
                return request;
            }

            if (Regex.IsMatch(memberName, ServicesConstants.EmailValidationRegex, RegexOptions.None, ServicesConstants.EmailValidationRegexTimeout))
            {
                throw new EntityException(ServicesStrings.AddMember_NameIsEmail);
            }

            var member = FindByUsername(memberName);
            if (member == null)
            {
                throw new EntityException(string.Format(CultureInfo.CurrentCulture,
                    ServicesStrings.AddMember_UserNotFound, memberName));
            }

            if (!member.Confirmed)
            {
                throw new EntityException(string.Format(CultureInfo.CurrentCulture,
                    ServicesStrings.AddMember_UserNotConfirmed, memberName));
            }

            if (member is Organization)
            {
                throw new EntityException(string.Format(CultureInfo.CurrentCulture,
                    ServicesStrings.AddMember_UserIsOrganization, memberName));
            }

            // Ensure that the new member meets the AAD tenant policy for this organization.
            var policyResult = await SecurityPolicyService.EvaluateOrganizationPoliciesAsync(
                SecurityPolicyAction.JoinOrganization, organization, member);
            if (policyResult != SecurityPolicyResult.SuccessResult)
            {
                throw new EntityException(policyResult.ErrorMessage);
            }

            request = new MembershipRequest()
            {
                Organization = organization,
                NewMember = member,
                IsAdmin = isAdmin,
                ConfirmationToken = Crypto.GenerateToken(),
                RequestDate = DateTime.UtcNow,
            };
            organization.MemberRequests.Add(request);

            await EntitiesContext.SaveChangesAsync();

            return request;
        }

        public async Task RejectMembershipRequestAsync(Organization organization, string memberName, string confirmationToken)
        {
            try
            {
                await DeleteMembershipRequestHelperAsync(organization, memberName, confirmationToken);
            }
            catch (InvalidOperationException)
            {
                throw new EntityException(string.Format(CultureInfo.CurrentCulture,
                    ServicesStrings.RejectMembershipRequest_NotFound, memberName));
            }
        }

        public async Task<User> CancelMembershipRequestAsync(Organization organization, string memberName)
        {
            try
            {
                return await DeleteMembershipRequestHelperAsync(organization, memberName);
            }
            catch (InvalidOperationException)
            {
                throw new EntityException(string.Format(CultureInfo.CurrentCulture,
                    ServicesStrings.CancelMembershipRequest_MissingRequest, memberName));
            }
        }

        private async Task<User> DeleteMembershipRequestHelperAsync(Organization organization, string memberName, string confirmationToken = null)
        {
            organization = organization ?? throw new ArgumentNullException(nameof(organization));

            var request = FindMembershipRequestByUsername(organization, memberName);
            if (request == null || (confirmationToken != null && request.ConfirmationToken != confirmationToken))
            {
                throw new InvalidOperationException("No such membership request exists!");
            }

            var pendingMember = request.NewMember;

            organization.MemberRequests.Remove(request);
            await EntitiesContext.SaveChangesAsync();

            return pendingMember;
        }

        public async Task<Membership> AddMemberAsync(Organization organization, string memberName, string confirmationToken)
        {
            organization = organization ?? throw new ArgumentNullException(nameof(organization));

            var request = FindMembershipRequestByUsername(organization, memberName);
            if (request == null || request.ConfirmationToken != confirmationToken)
            {
                throw new EntityException(string.Format(CultureInfo.CurrentCulture,
                    ServicesStrings.AddMember_MissingRequest, memberName));
            }

            var member = request.NewMember;

            organization.MemberRequests.Remove(request);

            if (!member.Confirmed)
            {
                throw new EntityException(string.Format(CultureInfo.CurrentCulture,
                    ServicesStrings.AddMember_UserNotConfirmed, memberName));
            }

            if (member is Organization)
            {
                throw new EntityException(string.Format(CultureInfo.CurrentCulture,
                    ServicesStrings.AddMember_UserIsOrganization, memberName));
            }

            var membership = FindMembershipByUsername(organization, memberName);
            if (membership == null)
            {
                // Ensure that the new member meets the AAD tenant policy for this organization.
                var policyResult = await SecurityPolicyService.EvaluateOrganizationPoliciesAsync(
                    SecurityPolicyAction.JoinOrganization, organization, member);
                if (policyResult != SecurityPolicyResult.SuccessResult)
                {
                    throw new EntityException(string.Format(CultureInfo.CurrentCulture,
                        ServicesStrings.AddMember_PolicyFailure, policyResult.ErrorMessage));
                }

                membership = new Membership()
                {
                    Member = member,
                    IsAdmin = request.IsAdmin
                };
                organization.Members.Add(membership);

                await Auditing.SaveAuditRecordAsync(new UserAuditRecord(organization, AuditedUserAction.AddOrganizationMember, membership));
            }
            else
            {
                // If the user is already a member, update the existing membership.
                // If the request grants admin but this member is not an admin, grant admin to the member.
                membership.IsAdmin = membership.IsAdmin || request.IsAdmin;

                await Auditing.SaveAuditRecordAsync(new UserAuditRecord(organization, AuditedUserAction.UpdateOrganizationMember, membership));
            }

            await EntitiesContext.SaveChangesAsync();

            return membership;
        }

        public async Task<Membership> UpdateMemberAsync(Organization organization, string memberName, bool isAdmin)
        {
            organization = organization ?? throw new ArgumentNullException(nameof(organization));

            var membership = FindMembershipByUsername(organization, memberName);
            if (membership == null)
            {
                throw new EntityException(string.Format(CultureInfo.CurrentCulture,
                    ServicesStrings.UpdateOrDeleteMember_MemberNotFound, memberName));
            }

            if (membership.IsAdmin != isAdmin)
            {
                // block removal of last admin
                if (membership.IsAdmin && organization.Administrators.Count() == 1)
                {
                    throw new EntityException(ServicesStrings.UpdateMember_CannotRemoveLastAdmin);
                }

                membership.IsAdmin = isAdmin;

                await Auditing.SaveAuditRecordAsync(new UserAuditRecord(organization, AuditedUserAction.UpdateOrganizationMember, membership));

                await EntitiesContext.SaveChangesAsync();
            }

            return membership;
        }

        public async Task<User> DeleteMemberAsync(Organization organization, string memberName)
        {
            organization = organization ?? throw new ArgumentNullException(nameof(organization));

            var membership = FindMembershipByUsername(organization, memberName);
            if (membership == null)
            {
                throw new EntityException(string.Format(CultureInfo.CurrentCulture,
                    ServicesStrings.UpdateOrDeleteMember_MemberNotFound, memberName));
            }

            var memberToRemove = membership.Member;

            // block removal of last admin
            if (membership.IsAdmin && organization.Administrators.Count() == 1)
            {
                throw new EntityException(ServicesStrings.DeleteMember_CannotRemoveLastAdmin);
            }

            organization.Members.Remove(membership);

            await Auditing.SaveAuditRecordAsync(new UserAuditRecord(organization, AuditedUserAction.RemoveOrganizationMember, membership));

            await EntitiesContext.SaveChangesAsync();

            return memberToRemove;
        }

        private Membership FindMembershipByUsername(Organization organization, string memberName)
        {
            return organization.Members
                .Where(m => m.Member.Username.Equals(memberName, StringComparison.OrdinalIgnoreCase))
                .SingleOrDefault();
        }

        private MembershipRequest FindMembershipRequestByUsername(Organization organization, string memberName)
        {
            return organization.MemberRequests
                .Where(m => m.NewMember.Username.Equals(memberName, StringComparison.OrdinalIgnoreCase))
                .SingleOrDefault();
        }

        public async Task ChangeEmailSubscriptionAsync(User user, bool emailAllowed, bool notifyPackagePushed)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.EmailAllowed = emailAllowed;
            user.NotifyPackagePushed = notifyPackagePushed;
            await UserRepository.CommitChangesAsync();
        }

        public virtual User FindByEmailAddress(string emailAddress)
        {
            var allMatches = UserRepository.GetAll()
                .Include(u => u.Credentials)
                .Include(u => u.Roles)
                .Where(u => u.EmailAddress == emailAddress)
                .Take(2)
                .ToList();

            if (allMatches.Count == 1)
            {
                return allMatches[0];
            }

            return null;
        }

        public virtual IList<User> FindAllByEmailAddress(string emailAddress)
        {
            return UserRepository.GetAll().Where(u => u.EmailAddress == emailAddress).ToList();
        }

        public virtual IList<User> FindByUnconfirmedEmailAddress(string unconfirmedEmailAddress, string optionalUsername)
        {
            if (optionalUsername == null)
            {
                return UserRepository.GetAll().Where(u => u.UnconfirmedEmailAddress == unconfirmedEmailAddress).ToList();
            }
            else
            {
                return UserRepository.GetAll().Where(u => u.UnconfirmedEmailAddress == unconfirmedEmailAddress && u.Username == optionalUsername).ToList();
            }
        }

        public virtual User FindByUsername(string username, bool includeDeleted = false)
        {
            var users = UserRepository.GetAll();
            if (!includeDeleted)
            {
                users = users.Where(u => !u.IsDeleted);
            }
            return users.Include(u => u.Roles)
                .Include(u => u.Credentials)
                .SingleOrDefault(u => u.Username == username);
        }

        public virtual User FindByKey(int key, bool includeDeleted = false)
        {
            var users = UserRepository.GetAll();
            if (!includeDeleted)
            {
                users = users.Where(u => !u.IsDeleted);
            }
            return users.Include(u => u.Roles)
                .Include(u => u.Credentials)
                .SingleOrDefault(u => u.Key == key);
        }

        public async Task ChangeEmailAddress(User user, string newEmailAddress)
        {
            var existingUsers = FindAllByEmailAddress(newEmailAddress);
            if (existingUsers.AnySafe(u => u.Key != user.Key))
            {
                throw new EntityException(ServicesStrings.EmailAddressBeingUsed, newEmailAddress);
            }

            await Auditing.SaveAuditRecordAsync(new UserAuditRecord(user, AuditedUserAction.ChangeEmail, newEmailAddress));

            user.UpdateUnconfirmedEmailAddress(newEmailAddress, Crypto.GenerateToken);

            if (!Config.ConfirmEmailAddresses)
            {
                user.ConfirmEmailAddress();
            }

            await UserRepository.CommitChangesAsync();
        }

        public async Task CancelChangeEmailAddress(User user)
        {
            await Auditing.SaveAuditRecordAsync(new UserAuditRecord(user, AuditedUserAction.CancelChangeEmail, user.UnconfirmedEmailAddress));

            user.CancelChangeEmailAddress();
            await UserRepository.CommitChangesAsync();
        }

        public virtual async Task ChangeMultiFactorAuthentication(User user, bool enableMultiFactor, string referrer = null)
        {
            await Auditing.SaveAuditRecordAsync(new UserAuditRecord(user, enableMultiFactor ? AuditedUserAction.EnabledMultiFactorAuthentication : AuditedUserAction.DisabledMultiFactorAuthentication));

            user.EnableMultiFactorAuthentication = enableMultiFactor;
            await UserRepository.CommitChangesAsync();

            TelemetryService.TrackUserChangedMultiFactorAuthentication(user, enableMultiFactor, referrer);
        }

        public async Task<IDictionary<int, string>> GetEmailAddressesForUserKeysAsync(IReadOnlyCollection<int> distinctUserKeys)
        {
            var results = await UserRepository.GetAll()
                .Where(u => distinctUserKeys.Contains(u.Key))
                .Select(u => new { u.Key, u.EmailAddress })
                .ToDictionaryAsync(u => u.Key, u => u.EmailAddress);

            return results;
        }

        public async Task<bool> ConfirmEmailAddress(User user, string token)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (String.IsNullOrEmpty(token))
            {
                throw new ArgumentNullException(nameof(token));
            }

            if (user.EmailConfirmationToken != token)
            {
                return false;
            }

            var conflictingUsers = FindAllByEmailAddress(user.UnconfirmedEmailAddress);
            if (conflictingUsers.AnySafe(u => u.Key != user.Key))
            {
                throw new EntityException(ServicesStrings.EmailAddressBeingUsed, user.UnconfirmedEmailAddress);
            }

            await Auditing.SaveAuditRecordAsync(new UserAuditRecord(user, AuditedUserAction.ConfirmEmail, user.UnconfirmedEmailAddress));

            user.ConfirmEmailAddress();

            await UserRepository.CommitChangesAsync();
            return true;
        }

        public async Task RequestTransformToOrganizationAccount(User accountToTransform, User adminUser)
        {
            accountToTransform = accountToTransform ?? throw new ArgumentNullException(nameof(accountToTransform));
            adminUser = adminUser ?? throw new ArgumentNullException(nameof(adminUser));

            // create new or update existing request
            if (accountToTransform.OrganizationMigrationRequest == null)
            {
                accountToTransform.OrganizationMigrationRequest = new OrganizationMigrationRequest();
            };

            accountToTransform.OrganizationMigrationRequest.NewOrganization = accountToTransform;
            accountToTransform.OrganizationMigrationRequest.AdminUser = adminUser;
            accountToTransform.OrganizationMigrationRequest.ConfirmationToken = Crypto.GenerateToken();
            accountToTransform.OrganizationMigrationRequest.RequestDate = DateTime.UtcNow;

            await UserRepository.CommitChangesAsync();
        }

        public bool CanTransformUserToOrganization(User accountToTransform, out string errorReason)
        {
            errorReason = null;

            if (!accountToTransform.Confirmed)
            {
                errorReason = String.Format(CultureInfo.CurrentCulture,
                    ServicesStrings.TransformAccount_AccountNotConfirmed, accountToTransform.Username);
            }
            else if (accountToTransform is Organization)
            {
                errorReason = String.Format(CultureInfo.CurrentCulture,
                    ServicesStrings.TransformAccount_AccountIsAnOrganization, accountToTransform.Username);
            }
            else if (accountToTransform.Organizations.Any() || accountToTransform.OrganizationRequests.Any())
            {
                errorReason = ServicesStrings.TransformAccount_AccountHasMemberships;
            }

            return errorReason == null;
        }

        public bool CanTransformUserToOrganization(User accountToTransform, User adminUser, out string errorReason)
        {
            if (!CanTransformUserToOrganization(accountToTransform, out errorReason))
            {
                return false;
            }

            if (adminUser.MatchesUser(accountToTransform))
            {
                errorReason = String.Format(CultureInfo.CurrentCulture,
                    ServicesStrings.TransformAccount_AdminMustBeDifferentAccount, adminUser.Username);
            }
            else if (!adminUser.Confirmed)
            {
                errorReason = String.Format(CultureInfo.CurrentCulture,
                    ServicesStrings.TransformAccount_AdminAccountNotConfirmed, adminUser.Username);
            }
            else if (adminUser is Organization)
            {
                errorReason = String.Format(CultureInfo.CurrentCulture,
                    ServicesStrings.TransformAccount_AdminAccountIsOrganization, adminUser.Username);
            }

            return errorReason == null;
        }

        public async Task<bool> TransformUserToOrganization(User accountToTransform, User adminUser, string token)
        {
            if (accountToTransform.OrganizationMigrationRequest == null
                || !accountToTransform.OrganizationMigrationRequest.AdminUser.MatchesUser(adminUser)
                || accountToTransform.OrganizationMigrationRequest.ConfirmationToken != token)
            {
                return false;
            }

            await SubscribeOrganizationToTenantPolicyIfTenantIdIsSupported(accountToTransform, adminUser);
            var result = await EntitiesContext.TransformUserToOrganization(accountToTransform, adminUser, token);
            if (result)
            {
                await Auditing.SaveAuditRecordAsync(new UserAuditRecord(accountToTransform, AuditedUserAction.TransformOrganization, adminUser, affectedMemberIsAdmin: true));
            }

            return result;
        }

        public async Task<Organization> AddOrganizationAsync(string username, string emailAddress, User adminUser)
        {
            var existingUserWithIdentity = EntitiesContext.Users
                .FirstOrDefault(u => u.Username == username || u.EmailAddress == emailAddress);
            if (existingUserWithIdentity != null)
            {
                if (existingUserWithIdentity.Username.Equals(username, StringComparison.OrdinalIgnoreCase))
                {
                    throw new EntityException(ServicesStrings.UsernameNotAvailable, username);
                }

                if (string.Equals(existingUserWithIdentity.EmailAddress, emailAddress, StringComparison.OrdinalIgnoreCase))
                {
                    throw new EntityException(ServicesStrings.EmailAddressBeingUsed, emailAddress);
                }
            }

            var organization = new Organization(username)
            {
                EmailAllowed = true,
                UnconfirmedEmailAddress = emailAddress,
                EmailConfirmationToken = Crypto.GenerateToken(),
                NotifyPackagePushed = true,
                CreatedUtc = DateTimeProvider.UtcNow,
                Members = new List<Membership>()
            };

            if (!Config.ConfirmEmailAddresses)
            {
                organization.ConfirmEmailAddress();
            }

            var membership = new Membership { Organization = organization, Member = adminUser, IsAdmin = true };

            organization.Members.Add(membership);
            adminUser.Organizations.Add(membership);

            OrganizationRepository.InsertOnCommit(organization);

            await SubscribeOrganizationToTenantPolicyIfTenantIdIsSupported(organization, adminUser, commitChanges: false);

            await Auditing.SaveAuditRecordAsync(new UserAuditRecord(organization, AuditedUserAction.AddOrganization, membership));

            await EntitiesContext.SaveChangesAsync();

            return organization;
        }

        private async Task SubscribeOrganizationToTenantPolicyIfTenantIdIsSupported(User organization, User adminUser, bool commitChanges = true)
        {
            var tenantId = adminUser.Credentials.GetAzureActiveDirectoryCredential()?.TenantId;
            if (string.IsNullOrEmpty(tenantId))
            {
                DiagnosticsSource.LogInformation("Will not apply tenant policy to organization because admin user does not have an AAD credential.");
                return;
            }

            if (!ContentObjectService.LoginDiscontinuationConfiguration.IsTenantIdPolicySupportedForOrganization(
                organization.EmailAddress ?? organization.UnconfirmedEmailAddress, 
                tenantId))
            {
                DiagnosticsSource.LogInformation("Will not apply tenant policy to organization because policy is not supported for email-tenant pair.");
                return;
            }

            DiagnosticsSource.LogInformation("Applying tenant policy to organization.");
            var tenantPolicy = RequireOrganizationTenantPolicy.Create(tenantId);
            await SecurityPolicyService.SubscribeAsync(organization, tenantPolicy, commitChanges);
        }

        public async Task<bool> RejectTransformUserToOrganizationRequest(User accountToTransform, User adminUser, string token)
        {
            var transformRequest = accountToTransform.OrganizationMigrationRequest;

            if (transformRequest == null)
            {
                return false;
            }

            if (transformRequest.AdminUser == null || !transformRequest.AdminUser.MatchesUser(adminUser))
            {
                return false;
            }

            if (transformRequest.ConfirmationToken != token)
            {
                return false;
            }

            accountToTransform.OrganizationMigrationRequest = null;

            await UserRepository.CommitChangesAsync();

            return true;
        }

        public async Task<bool> CancelTransformUserToOrganizationRequest(User accountToTransform, string token)
        {
            var transformRequest = accountToTransform.OrganizationMigrationRequest;

            if (transformRequest == null)
            {
                return false;
            }

            if (transformRequest.ConfirmationToken != token)
            {
                return false;
            }

            accountToTransform.OrganizationMigrationRequest = null;

            await UserRepository.CommitChangesAsync();

            return true;
        }

        public IReadOnlyList<User> GetSiteAdmins()
        {
            return GetAdminRole()
                .Users
                .Where(u => !u.IsDeleted)
                .ToList();
        }

        public Task SetIsAdministrator(User user, bool isAdmin)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var adminRole = GetAdminRole();
            if (adminRole == null)
            {
                throw new InvalidOperationException("No admin role exists!");
            }

            if (isAdmin)
            {
                adminRole.Users.Add(user);
                user.Roles.Add(adminRole);
            }
            else
            {
                adminRole.Users.Remove(user);
                user.Roles.Remove(adminRole);
            }

            return RoleRepository.CommitChangesAsync();
        }

        private Role GetAdminRole()
        {
            return RoleRepository.GetAll()
                .ToList()
                .Single(r => r.Is(Constants.AdminRoleName));
        }
    }
}