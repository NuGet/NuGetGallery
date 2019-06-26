﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication;
using NuGetGallery.Features;
using NuGetGallery.Security;

namespace NuGetGallery
{
    public class DeleteAccountService : IDeleteAccountService
    {
        private readonly IEntityRepository<AccountDelete> _accountDeleteRepository;
        private readonly IEntityRepository<PackageDelete> _packageDeleteRepository;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IPackageService _packageService;
        private readonly IPackageUpdateService _packageUpdateService;
        private readonly IPackageOwnershipManagementService _packageOwnershipManagementService;
        private readonly IReservedNamespaceService _reservedNamespaceService;
        private readonly ISecurityPolicyService _securityPolicyService;
        private readonly IAuthenticationService _authService;
        private readonly IEntityRepository<PackageDeprecation> _deprecationRepository;
        private readonly IEntityRepository<User> _userRepository;
        private readonly IEntityRepository<Scope> _scopeRepository;
        private readonly ISupportRequestService _supportRequestService;
        private readonly IEditableFeatureFlagStorageService _featureFlagService;
        private readonly IAuditingService _auditingService;
        private readonly ITelemetryService _telemetryService;

        public DeleteAccountService(
            IEntityRepository<AccountDelete> accountDeleteRepository,
            IEntityRepository<PackageDelete> packageDeleteRepository,
            IEntityRepository<PackageDeprecation> deprecationRepository,
            IEntityRepository<User> userRepository,
            IEntityRepository<Scope> scopeRepository,
            IEntitiesContext entitiesContext,
            IPackageService packageService,
            IPackageUpdateService packageUpdateService,
            IPackageOwnershipManagementService packageOwnershipManagementService,
            IReservedNamespaceService reservedNamespaceService,
            ISecurityPolicyService securityPolicyService,
            IAuthenticationService authService,
            ISupportRequestService supportRequestService,
            IEditableFeatureFlagStorageService featureFlagService,
            IAuditingService auditingService,
            ITelemetryService telemetryService)
        {
            _accountDeleteRepository = accountDeleteRepository ?? throw new ArgumentNullException(nameof(accountDeleteRepository));
            _packageDeleteRepository = packageDeleteRepository ?? throw new ArgumentNullException(nameof(packageDeleteRepository));
            _deprecationRepository = deprecationRepository ?? throw new ArgumentNullException(nameof(deprecationRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _scopeRepository = scopeRepository ?? throw new ArgumentNullException(nameof(scopeRepository));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _packageUpdateService = packageUpdateService ?? throw new ArgumentNullException(nameof(packageUpdateService));
            _packageOwnershipManagementService = packageOwnershipManagementService ?? throw new ArgumentNullException(nameof(packageOwnershipManagementService));
            _reservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));
            _securityPolicyService = securityPolicyService ?? throw new ArgumentNullException(nameof(securityPolicyService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _supportRequestService = supportRequestService ?? throw new ArgumentNullException(nameof(supportRequestService));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        }

        public async Task<DeleteAccountStatus> DeleteAccountAsync(User userToBeDeleted,
            User userToExecuteTheDelete,
            AccountDeletionOrphanPackagePolicy orphanPackagePolicy = AccountDeletionOrphanPackagePolicy.DoNotAllowOrphans)
        {
            if (userToBeDeleted == null)
            {
                throw new ArgumentNullException(nameof(userToBeDeleted));
            }

            if (userToExecuteTheDelete == null)
            {
                throw new ArgumentNullException(nameof(userToExecuteTheDelete));
            }

            if (userToBeDeleted.IsDeleted)
            {
                return new DeleteAccountStatus()
                {
                    Success = false,
                    Description = string.Format(CultureInfo.CurrentCulture,
                        ServicesStrings.AccountDelete_AccountAlreadyDeleted,
                        userToBeDeleted.Username),
                    AccountName = userToBeDeleted.Username
                };
            }
            
            var status = await RunAccountDeletionTask(
                () => DeleteAccountImplAsync(
                    userToBeDeleted, 
                    userToExecuteTheDelete,
                    orphanPackagePolicy),
                userToBeDeleted,
                userToExecuteTheDelete);

            _telemetryService.TrackAccountDeletionCompleted(userToBeDeleted, userToExecuteTheDelete, status.Success);
            return status;
        }

        private async Task DeleteAccountImplAsync(User userToBeDeleted, User userToExecuteTheDelete, AccountDeletionOrphanPackagePolicy orphanPackagePolicy, bool commitChanges = true)
        {
            await RemoveReservedNamespaces(userToBeDeleted);
            await RemovePackageOwnership(userToBeDeleted, userToExecuteTheDelete, orphanPackagePolicy);
            await RemoveMemberships(userToBeDeleted, userToExecuteTheDelete, orphanPackagePolicy);
            await RemoveSecurityPolicies(userToBeDeleted);
            await RemoveUserCredentials(userToBeDeleted);
            await RemovePackageOwnershipRequests(userToBeDeleted);
            ResetPackagesAndAccountsDeletedBy(userToBeDeleted);

            RemovePackagePushedBy(userToBeDeleted);
            RemovePackageDeprecatedBy(userToBeDeleted);

            var organizationToBeDeleted = userToBeDeleted as Organization;
            if (organizationToBeDeleted != null)
            {
                RemoveMembers(organizationToBeDeleted);
            }

            if (!userToBeDeleted.Confirmed)
            {
                // Unconfirmed users should be hard-deleted.
                // Another account with the same username can be created.
                RemoveUser(userToBeDeleted);
            }
            else
            {
                // Confirmed users should be soft-deleted.
                // Another account with the same username cannot be created.
                RemoveUserDataInUserTable(userToBeDeleted);
                InsertDeleteAccount(
                    userToBeDeleted, 
                    userToExecuteTheDelete);
            }

            if (commitChanges)
            {
                await _entitiesContext.SaveChangesAsync();
            }
        }

        private void InsertDeleteAccount(User user, User admin)
        {
            var accountDelete = new AccountDelete
            {
                DeletedOn = DateTime.UtcNow,
                DeletedAccountKey = user.Key,
                DeletedByKey = admin.Key,
            };

            _accountDeleteRepository.InsertOnCommit(accountDelete);
        }

        private async Task RemoveUserCredentials(User user)
        {
            // Remove any credential owned by this user.
            var userCredentials = user.Credentials.ToList();

            // Remove any credential scoped to this user.
            var credentialsScopedToUser = _scopeRepository
                .GetAll()
                .Where(s => s.OwnerKey == user.Key)
                .Select(s => s.Credential)
                .ToList();

            var credentials = userCredentials
                .Concat(credentialsScopedToUser)
                .Distinct()
                .ToList();

            foreach (var credential in credentials)
            {
                await _authService.RemoveCredential(credential.User, credential, commitChanges: false);
            }
        }

        private async Task RemoveSecurityPolicies(User user)
        {
            foreach (var usp in user.SecurityPolicies.ToList())
            {
                await _securityPolicyService.UnsubscribeAsync(user, usp.Subscription, commitChanges: false);
            }
        }

        private async Task RemoveReservedNamespaces(User user)
        {
            foreach (var rn in user.ReservedNamespaces.ToList())
            {
                await _reservedNamespaceService.DeleteOwnerFromReservedNamespaceAsync(rn.Value, user.Username, commitChanges: false);
            }
        }

        private async Task RemovePackageOwnership(User user, User requestingUser, AccountDeletionOrphanPackagePolicy orphanPackagePolicy)
        {
            foreach (var packageRegistration in GetPackageRegistrationsOwnedByUser(user))
            {
                if (_packageService.WillPackageBeOrphanedIfOwnerRemoved(packageRegistration, user))
                {
                    if (orphanPackagePolicy == AccountDeletionOrphanPackagePolicy.DoNotAllowOrphans)
                    {
                        throw new InvalidOperationException($"Deleting user '{user.Username}' will make package '{packageRegistration.Id}' an orphan, but no orphans were expected.");
                    }
                    else if (orphanPackagePolicy == AccountDeletionOrphanPackagePolicy.UnlistOrphans)
                    {
                        foreach (var package in packageRegistration.Packages)
                        {
                            await _packageUpdateService.MarkPackageUnlistedAsync(package, commitChanges: false, updateIndex: false);
                        }
                    }
                }

                await _packageOwnershipManagementService.RemovePackageOwnerAsync(packageRegistration, requestingUser, user, commitChanges: false);
            }
        }

        private void RemovePackagePushedBy(User user)
        {
            foreach (var package in _entitiesContext
                .Packages
                .Where(p => p.UserKey == user.Key)
                .ToList())
            {
                package.User = null;
            }
        }

        private List<PackageRegistration> GetPackageRegistrationsOwnedByUser(User user)
        {
            return _packageService
                .FindPackageRegistrationsByOwner(user)
                .ToList();
        }

        private async Task RemovePackageOwnershipRequests(User user)
        {
            var toRequests = _packageOwnershipManagementService
                .GetPackageOwnershipRequests(newOwner: user)
                .ToList();

            var fromRequests = _packageOwnershipManagementService
                .GetPackageOwnershipRequests(requestingOwner: user)
                .ToList();

            var requests = toRequests.Concat(fromRequests).ToList();

            foreach (var request in requests)
            {
                await _packageOwnershipManagementService.DeletePackageOwnershipRequestAsync(request.PackageRegistration, request.NewOwner, commitChanges: false);
            }
        }

        private void RemovePackageDeprecatedBy(User user)
        {
            foreach (var deprecation in _deprecationRepository
                .GetAll()
                .Where(d => d.DeprecatedByUserKey == user.Key)
                .ToList())
            {
                deprecation.DeprecatedByUser = null;
            }
        }
        
        private async Task RemoveMemberships(User user, User requestingUser, AccountDeletionOrphanPackagePolicy orphanPackagePolicy)
        {
            foreach (var membership in user.Organizations.ToList())
            {
                user.Organizations.Remove(membership);
                var organization = membership.Organization;
                var otherMembers = organization.Members
                    .Where(m => !m.Member.MatchesUser(user));

                if (!otherMembers.Any())
                {
                    // The user we are deleting is the only member of the organization.
                    // We should delete the entire organization.
                    await DeleteAccountImplAsync(organization, requestingUser, orphanPackagePolicy, commitChanges: false);
                }
                else if (otherMembers.All(m => !m.IsAdmin))
                {
                    // All other members of this organization are collaborators, so we should promote them to administrators.
                    foreach (var collaborator in otherMembers)
                    {
                        collaborator.IsAdmin = true;
                    }
                }
            }

            foreach (var membershipRequest in user.OrganizationRequests.ToList())
            {
                user.OrganizationRequests.Remove(membershipRequest);
            }

            foreach (var transformationRequest in user.OrganizationMigrationRequests.ToList())
            {
                user.OrganizationMigrationRequests.Remove(transformationRequest);
                transformationRequest.NewOrganization.OrganizationMigrationRequest = null;
            }

            var migrationRequest = user.OrganizationMigrationRequest;
            user.OrganizationMigrationRequest = null;
            if (migrationRequest != null)
            {
                migrationRequest.AdminUser.OrganizationMigrationRequests.Remove(migrationRequest);
            }
        }

        private void RemoveMembers(Organization organization)
        {
            foreach (var membership in organization.Members.ToList())
            {
                organization.Members.Remove(membership);
            }

            foreach (var memberRequest in organization.MemberRequests.ToList())
            {
                organization.MemberRequests.Remove(memberRequest);
            }
        }

        private void ResetPackagesAndAccountsDeletedBy(User user)
        {
            foreach (var deletedPackage in _packageDeleteRepository
                .GetAll()
                .Where(d => d.DeletedByKey == user.Key)
                .ToList())
            {
                deletedPackage.DeletedBy = null;
            }

            foreach (var deletedAccount in _accountDeleteRepository
                .GetAll()
                .Where(d => d.DeletedByKey == user.Key)
                .ToList())
            {
                deletedAccount.DeletedBy = null;
            }
        }

        private void RemoveUserDataInUserTable(User user)
        {
            user.SetAccountAsDeleted();
        }

        private async Task RemoveSupportRequests(User user)
        {
            await _supportRequestService.DeleteSupportRequestsAsync(user);
        }

        private void RemoveUser(User user)
        {
            _userRepository.DeleteOnCommit(user);
        }

        private async Task<DeleteAccountStatus> RunAccountDeletionTask(Func<Task> getTask, User userToBeDeleted, User requestingUser)
        {
            try
            {
                // The support requests DB and gallery DB are different.
                // TransactionScope can be used for doing transaction actions across db on the same server but not on different servers.
                // The below code will clean the feature flags and suppport requests before the gallery data.
                // The order is important in order to allow the admin the opportunity to execute this step again.
                await _featureFlagService.RemoveUserAsync(userToBeDeleted);
                await RemoveSupportRequests(userToBeDeleted);

                using (var strategy = new SuspendDbExecutionStrategy())
                using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
                {
                    await getTask();
                    transaction.Commit();
                }

                await _auditingService.SaveAuditRecordAsync(new DeleteAccountAuditRecord(username: userToBeDeleted.Username,
                    status: DeleteAccountAuditRecord.ActionStatus.Success,
                    action: AuditedDeleteAccountAction.DeleteAccount,
                    adminUsername: requestingUser.Username));

                return new DeleteAccountStatus()
                {
                    Success = true,
                    Description = string.Format(CultureInfo.CurrentCulture,
                        ServicesStrings.AccountDelete_Success,
                        userToBeDeleted.Username),
                    AccountName = userToBeDeleted.Username
                };
            }
            catch (Exception e)
            {
                QuietLog.LogHandledException(e);
                return new DeleteAccountStatus()
                {
                    Success = false,
                    Description = string.Format(CultureInfo.CurrentCulture,
                        ServicesStrings.AccountDelete_Fail,
                        userToBeDeleted.Username, e),
                    AccountName = userToBeDeleted.Username
                };
            }
        }
    }
}