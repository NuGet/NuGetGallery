// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using NuGetGallery.Security;
using Crypto = NuGetGallery.CryptographyService;

namespace NuGetGallery
{
    public class UserService : IUserService
    {
        public IAppConfiguration Config { get; protected set; }

        public IEntityRepository<User> UserRepository { get; protected set; }

        public IEntityRepository<Credential> CredentialRepository { get; protected set; }

        public IEntityRepository<Organization> OrganizationRepository { get; protected set; }

        public IAuditingService Auditing { get; protected set; }

        public IEntitiesContext EntitiesContext { get; protected set; }

        public IContentObjectService ContentObjectService { get; protected set; }

        public ISecurityPolicyService SecurityPolicyService { get; set; }

        public IDateTimeProvider DateTimeProvider { get; protected set; }

        protected UserService() { }

        public UserService(
            IAppConfiguration config,
            IEntityRepository<User> userRepository,
            IEntityRepository<Credential> credentialRepository,
            IEntityRepository<Organization> organizationRepository,
            IAuditingService auditing,
            IEntitiesContext entitiesContext,
            IContentObjectService contentObjectService,
            ISecurityPolicyService securityPolicyService,
            IDateTimeProvider dateTimeProvider)
            : this()
        {
            Config = config;
            UserRepository = userRepository;
            CredentialRepository = credentialRepository;
            OrganizationRepository = organizationRepository;
            Auditing = auditing;
            EntitiesContext = entitiesContext;
            ContentObjectService = contentObjectService;
            SecurityPolicyService = securityPolicyService;
            DateTimeProvider = dateTimeProvider;
        }

        public async Task<Membership> AddMemberAsync(Organization organization, string memberName, bool isAdmin)
        {
            organization = organization ?? throw new ArgumentNullException(nameof(organization));

            var membership = FindMembershipByUsername(organization, memberName);
            if (membership != null)
            {
                throw new EntityException(string.Format(CultureInfo.CurrentCulture,
                    Strings.AddMember_AlreadyAMember, memberName));
            }

            var member = FindByUsername(memberName);
            if (member == null)
            {
                throw new EntityException(string.Format(CultureInfo.CurrentCulture,
                    Strings.AddMember_UserNotFound, memberName));
            }

            if (!member.Confirmed)
            {
                throw new EntityException(string.Format(CultureInfo.CurrentCulture,
                    Strings.AddMember_UserNotConfirmed, memberName));
            }

            // Ensure that the new member meets the AAD tenant policy for this organization.
            var policyResult = await SecurityPolicyService.EvaluateOrganizationPoliciesAsync(
                SecurityPolicyAction.JoinOrganization, organization, member);
            if (policyResult != SecurityPolicyResult.SuccessResult)
            {
                throw new EntityException(policyResult.ErrorMessage);
            }

            membership = new Membership()
            {
                Member = member,
                IsAdmin = isAdmin
            };
            organization.Members.Add(membership);

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
                    Strings.UpdateOrDeleteMember_MemberNotFound, memberName));
            }

            if (membership.IsAdmin != isAdmin)
            {
                // block removal of last admin
                if (membership.IsAdmin && organization.Administrators.Count() == 1)
                {
                    throw new EntityException(Strings.UpdateMember_CannotRemoveLastAdmin);
                }

                membership.IsAdmin = isAdmin;
                await EntitiesContext.SaveChangesAsync();
            }

            return membership;
        }

        public async Task DeleteMemberAsync(Organization organization, string memberName)
        {
            organization = organization ?? throw new ArgumentNullException(nameof(organization));

            var membership = FindMembershipByUsername(organization, memberName);
            if (membership == null)
            {
                throw new EntityException(string.Format(CultureInfo.CurrentCulture,
                    Strings.UpdateOrDeleteMember_MemberNotFound, memberName));
            }

            // block removal of last admin
            if (membership.IsAdmin && organization.Administrators.Count() == 1)
            {
                throw new EntityException(Strings.DeleteMember_CannotRemoveLastAdmin);
            }

            organization.Members.Remove(membership);
            await EntitiesContext.SaveChangesAsync();
        }

        private Membership FindMembershipByUsername(Organization organization, string memberName)
        {
            return organization.Members
                .Where(m => m.Member.Username.Equals(memberName, StringComparison.OrdinalIgnoreCase))
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

        public virtual User FindByUsername(string username)
        {
            return UserRepository.GetAll()
                .Include(u => u.Roles)
                .Include(u => u.Credentials)
                .SingleOrDefault(u => u.Username == username);
        }

        public virtual User FindByKey(int key)
        {
            return UserRepository.GetAll()
                .Include(u => u.Roles)
                .Include(u => u.Credentials)
                .SingleOrDefault(u => u.Key == key);
        }

        public async Task ChangeEmailAddress(User user, string newEmailAddress)
        {
            var existingUsers = FindAllByEmailAddress(newEmailAddress);
            if (existingUsers.AnySafe(u => u.Key != user.Key))
            {
                throw new EntityException(Strings.EmailAddressBeingUsed, newEmailAddress);
            }

            await Auditing.SaveAuditRecordAsync(new UserAuditRecord(user, AuditedUserAction.ChangeEmail, newEmailAddress));

            user.UpdateEmailAddress(newEmailAddress, Crypto.GenerateToken);
            await UserRepository.CommitChangesAsync();
        }

        public async Task CancelChangeEmailAddress(User user)
        {
            await Auditing.SaveAuditRecordAsync(new UserAuditRecord(user, AuditedUserAction.CancelChangeEmail, user.UnconfirmedEmailAddress));

            user.CancelChangeEmailAddress();
            await UserRepository.CommitChangesAsync();
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
                throw new EntityException(Strings.EmailAddressBeingUsed, user.UnconfirmedEmailAddress);
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
                    Strings.TransformAccount_AccountNotConfirmed, accountToTransform.Username);
            }
            else if (accountToTransform is Organization)
            {
                errorReason = String.Format(CultureInfo.CurrentCulture,
                    Strings.TransformAccount_AccountIsAnOrganization, accountToTransform.Username);
            }
            else if (accountToTransform.Organizations.Any() || accountToTransform.OrganizationRequests.Any())
            {
                errorReason = Strings.TransformAccount_AccountHasMemberships;
            }
            else if (!ContentObjectService.LoginDiscontinuationConfiguration.AreOrganizationsSupportedForUser(accountToTransform))
            {
                errorReason = String.Format(CultureInfo.CurrentCulture,
                    Strings.Organizations_NotInDomainWhitelist, accountToTransform.Username);
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
                    Strings.TransformAccount_AdminMustBeDifferentAccount, adminUser.Username);
            }
            else if (!adminUser.Confirmed)
            {
                errorReason = String.Format(CultureInfo.CurrentCulture,
                    Strings.TransformAccount_AdminAccountNotConfirmed, adminUser.Username);
            }
            else if (adminUser is Organization)
            {
                errorReason = String.Format(CultureInfo.CurrentCulture,
                    Strings.TransformAccount_AdminAccountIsOrganization, adminUser.Username);
            }
            else
            {
                var tenantId = GetAzureActiveDirectoryCredentialTenant(adminUser);
                if (string.IsNullOrWhiteSpace(tenantId))
                {
                    errorReason = String.Format(CultureInfo.CurrentCulture,
                        Strings.Organizations_AdminAccountDoesNotHaveTenant, adminUser.Username);
                }
            }

            return errorReason == null;
        }

        public async Task<bool> TransformUserToOrganization(User accountToTransform, User adminUser, string token)
        {
            if (!await SubscribeOrganizationToTenantPolicy(accountToTransform, adminUser))
            {
                return false;
            }
            
            return await EntitiesContext.TransformUserToOrganization(accountToTransform, adminUser, token);
        }

        public async Task<Organization> AddOrganizationAsync(string username, string emailAddress, User adminUser)
        {
            if (!ContentObjectService.LoginDiscontinuationConfiguration.AreOrganizationsSupportedForUser(adminUser))
            {
                throw new EntityException(String.Format(CultureInfo.CurrentCulture,
                    Strings.Organizations_NotInDomainWhitelist, adminUser.Username));
            }
            
            var existingUserWithIdentity = EntitiesContext.Users
                .FirstOrDefault(u => u.Username == username || u.EmailAddress == emailAddress);
            if (existingUserWithIdentity != null)
            {
                if (existingUserWithIdentity.Username.Equals(username, StringComparison.OrdinalIgnoreCase))
                {
                    throw new EntityException(Strings.UsernameNotAvailable, username);
                }

                if (string.Equals(existingUserWithIdentity.EmailAddress, emailAddress, StringComparison.OrdinalIgnoreCase))
                {
                    throw new EntityException(Strings.EmailAddressBeingUsed, emailAddress);
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

            var membership = new Membership { Organization = organization, Member = adminUser, IsAdmin = true };

            organization.Members.Add(membership);
            adminUser.Organizations.Add(membership);

            OrganizationRepository.InsertOnCommit(organization);

            if (string.IsNullOrEmpty(GetAzureActiveDirectoryCredentialTenant(adminUser)))
            {
                throw new EntityException(String.Format(CultureInfo.CurrentCulture,
                        Strings.Organizations_AdminAccountDoesNotHaveTenant, adminUser.Username));
            }
            
            if (!await SubscribeOrganizationToTenantPolicy(organization, adminUser, commitChanges: false))
            {
                throw new EntityException(Strings.DefaultUserSafeExceptionMessage);
            }

            await EntitiesContext.SaveChangesAsync();

            return organization;
        }

        private async Task<bool> SubscribeOrganizationToTenantPolicy(User organization, User adminUser, bool commitChanges = true)
        {
            var tenantId = GetAzureActiveDirectoryCredentialTenant(adminUser);
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return false;
            }

            var tenantPolicy = RequireOrganizationTenantPolicy.Create(tenantId);
            if (!await SecurityPolicyService.SubscribeAsync(organization, tenantPolicy, commitChanges))
            {
                return false;
            }

            return true;
        }

        private string GetAzureActiveDirectoryCredentialTenant(User user)
        {
            return user.Credentials.GetAzureActiveDirectoryCredential()?.TenantId;
        }
    }
}