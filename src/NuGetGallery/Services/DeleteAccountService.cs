// Copyright (c) .NET Foundation. All rights reserved.
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
        private readonly IEntitiesContext _entitiesContext;
        private readonly IPackageService _packageService;
        private readonly IPackageOwnershipManagementService _packageOwnershipManagementService;
        private readonly IReservedNamespaceService _reservedNamespaceService;
        private readonly ISecurityPolicyService _securityPolicyService;
        private readonly AuthenticationService _authService;
        private readonly IEntityRepository<User> _userRepository;
        private readonly IEntityRepository<Scope> _scopeRepository;
        private readonly ISupportRequestService _supportRequestService;
        private readonly IEditableFeatureFlagStorageService _featureFlagService;
        private readonly IAuditingService _auditingService;
        private readonly ITelemetryService _telemetryService;

        public DeleteAccountService(IEntityRepository<AccountDelete> accountDeleteRepository,
                                    IEntityRepository<User> userRepository,
                                    IEntityRepository<Scope> scopeRepository,
                                    IEntitiesContext entitiesContext,
                                    IPackageService packageService,
                                    IPackageOwnershipManagementService packageOwnershipManagementService,
                                    IReservedNamespaceService reservedNamespaceService,
                                    ISecurityPolicyService securityPolicyService,
                                    AuthenticationService authService,
                                    ISupportRequestService supportRequestService,
                                    IEditableFeatureFlagStorageService featureFlagService,
                                    IAuditingService auditingService,
                                    ITelemetryService telemetryService
            )
        {
            _accountDeleteRepository = accountDeleteRepository ?? throw new ArgumentNullException(nameof(accountDeleteRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _scopeRepository = scopeRepository ?? throw new ArgumentNullException(nameof(scopeRepository));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _packageOwnershipManagementService = packageOwnershipManagementService ?? throw new ArgumentNullException(nameof(packageOwnershipManagementService));
            _reservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));
            _securityPolicyService = securityPolicyService ?? throw new ArgumentNullException(nameof(securityPolicyService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _supportRequestService = supportRequestService ?? throw new ArgumentNullException(nameof(supportRequestService));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        }

        public async Task<DeleteUserAccountStatus> DeleteAccountAsync(User userToBeDeleted,
            User userToExecuteTheDelete,
            bool commitAsTransaction,
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
                return new DeleteUserAccountStatus()
                {
                    Success = false,
                    Description = string.Format(CultureInfo.CurrentCulture,
                        Strings.AccountDelete_AccountAlreadyDeleted,
                        userToBeDeleted.Username),
                    AccountName = userToBeDeleted.Username
                };
            }
            
            var deleteUserAccountStatus = await RunAccountDeletionTask(
                () => DeleteAccountImplAsync(
                    userToBeDeleted, 
                    userToExecuteTheDelete,
                    orphanPackagePolicy),
                userToBeDeleted,
                userToExecuteTheDelete,
                commitAsTransaction);

            _telemetryService.TrackAccountDeletionCompleted(userToBeDeleted, userToExecuteTheDelete, deleteUserAccountStatus.Success);
            return deleteUserAccountStatus;
        }

        private async Task DeleteAccountImplAsync(User userToBeDeleted, User userToExecuteTheDelete, AccountDeletionOrphanPackagePolicy orphanPackagePolicy)
        {
            await RemoveReservedNamespaces(userToBeDeleted);
            await RemovePackageOwnership(userToBeDeleted, userToExecuteTheDelete, orphanPackagePolicy);
            await RemoveMemberships(userToBeDeleted, userToExecuteTheDelete, orphanPackagePolicy);
            await RemoveSecurityPolicies(userToBeDeleted);
            await RemoveUserCredentials(userToBeDeleted);
            await RemovePackageOwnershipRequests(userToBeDeleted);

            var organizationToBeDeleted = userToBeDeleted as Organization;
            if (organizationToBeDeleted != null)
            {
                await RemoveMembers(organizationToBeDeleted);
            }

            if (!userToBeDeleted.Confirmed)
            {
                // Unconfirmed users should be hard-deleted.
                // Another account with the same username can be created.
                await RemoveUser(userToBeDeleted);
            }
            else
            {
                // Confirmed users should be soft-deleted.
                // Another account with the same username cannot be created.
                await RemoveUserDataInUserTable(userToBeDeleted);
                await InsertDeleteAccount(
                    userToBeDeleted, 
                    userToExecuteTheDelete);
            }
        }

        private async Task InsertDeleteAccount(User user, User admin)
        {
            var accountDelete = new AccountDelete
            {
                DeletedOn = DateTime.UtcNow,
                DeletedAccountKey = user.Key,
                DeletedByKey = admin.Key,
            };
            _accountDeleteRepository.InsertOnCommit(accountDelete);
            await _accountDeleteRepository.CommitChangesAsync();
        }

        private async Task RemoveUserCredentials(User user)
        {
            // Remove any credential owned by this user.
            foreach (var uc in user.Credentials.ToArray())
            {
                await _authService.RemoveCredential(user, uc);
            }

            // Remove any credential scoped to this user.
            var userScopes = _scopeRepository
                .GetAll()
                .Where(s => s.OwnerKey == user.Key)
                .ToArray();

            var credentials = userScopes.Select(s => s.Credential).Distinct().ToArray();
            foreach (var credential in credentials)
            {
                await _authService.RemoveCredential(credential.User, credential);
            }
        }

        private async Task RemoveSecurityPolicies(User user)
        {
            foreach (var usp in user.SecurityPolicies.ToArray())
            {
                await _securityPolicyService.UnsubscribeAsync(user, usp.Subscription);
            }
        }

        private async Task RemoveReservedNamespaces(User user)
        {
            foreach (var rn in user.ReservedNamespaces.ToArray())
            {
                await _reservedNamespaceService.DeleteOwnerFromReservedNamespaceAsync(rn.Value, user.Username, commitAsTransaction:false);
            }
        }

        private async Task RemovePackageOwnership(User user, User requestingUser, AccountDeletionOrphanPackagePolicy orphanPackagePolicy)
        {
            foreach (var package in GetPackagesOwnedByUser(user))
            {
                if (_packageService.WillPackageBeOrphanedIfOwnerRemoved(package.PackageRegistration, user))
                {
                    if (orphanPackagePolicy == AccountDeletionOrphanPackagePolicy.DoNotAllowOrphans)
                    {
                        throw new InvalidOperationException($"Deleting user '{user.Username}' will make package '{package.PackageRegistration.Id}' an orphan, but no orphans were expected.");
                    }
                    else if (orphanPackagePolicy == AccountDeletionOrphanPackagePolicy.UnlistOrphans)
                    {
                        await _packageService.MarkPackageUnlistedAsync(package, commitChanges: true);
                    }
                }

                await _packageOwnershipManagementService.RemovePackageOwnerAsync(package.PackageRegistration, requestingUser, user, commitAsTransaction:false);
            }
        }

        private List<Package> GetPackagesOwnedByUser(User user)
        {
            return _packageService.FindPackagesByAnyMatchingOwner(user, includeUnlisted: true, includeVersions: true).ToList();
        }

        private async Task RemovePackageOwnershipRequests(User user)
        {
            var requests = _packageOwnershipManagementService.GetPackageOwnershipRequests(newOwner: user).ToList();
            foreach (var request in requests)
            {
                await _packageOwnershipManagementService.DeletePackageOwnershipRequestAsync(request.PackageRegistration, request.NewOwner);
            }
        }
        
        private async Task RemoveMemberships(User user, User requestingUser, AccountDeletionOrphanPackagePolicy orphanPackagePolicy)
        {
            foreach (var membership in user.Organizations.ToArray())
            {
                user.Organizations.Remove(membership);
                var organization = membership.Organization;
                var otherMembers = organization.Members
                    .Where(m => !m.Member.MatchesUser(user));

                if (!otherMembers.Any())
                {
                    // The user we are deleting is the only member of the organization.
                    // We should delete the entire organization.
                    await DeleteAccountImplAsync(organization, requestingUser, orphanPackagePolicy);
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

            foreach (var membershipRequest in user.OrganizationRequests.ToArray())
            {
                user.OrganizationRequests.Remove(membershipRequest);
            }

            foreach (var transformationRequest in user.OrganizationMigrationRequests.ToArray())
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

            await _entitiesContext.SaveChangesAsync();
        }

        private async Task RemoveMembers(Organization organization)
        {
            foreach (var membership in organization.Members.ToList())
            {
                organization.Members.Remove(membership);
            }

            foreach (var memberRequest in organization.MemberRequests.ToList())
            {
                organization.MemberRequests.Remove(memberRequest);
            }

            await _entitiesContext.SaveChangesAsync();
        }

        private async Task RemoveUserDataInUserTable(User user)
        {
            user.SetAccountAsDeleted();
            await _userRepository.CommitChangesAsync();
        }

        private async Task RemoveSupportRequests(User user)
        {
            await _supportRequestService.DeleteSupportRequestsAsync(user);
        }

        private async Task RemoveUser(User user)
        {
            _userRepository.DeleteOnCommit(user);
            await _userRepository.CommitChangesAsync();
        }

        private async Task<DeleteUserAccountStatus> RunAccountDeletionTask(Func<Task> getTask, User userToBeDeleted, User requestingUser, bool commitAsTransaction)
        {
            try
            {
                // The support requests DB and gallery DB are different.
                // TransactionScope can be used for doing transaction actions across db on the same server but not on different servers.
                // The below code will clean the feature flags and suppport requests before the gallery data.
                // The order is important in order to allow the admin the opportunity to execute this step again.
                if (!await _featureFlagService.TryRemoveUserAsync(userToBeDeleted))
                {
                    return new DeleteUserAccountStatus()
                    {
                        Success = false,
                        Description = string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.AccountDelete_FailRetryable,
                            userToBeDeleted.Username),
                        AccountName = userToBeDeleted.Username
                    };
                }

                await RemoveSupportRequests(userToBeDeleted);

                if (commitAsTransaction)
                {
                    using (var strategy = new SuspendDbExecutionStrategy())
                    using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
                    {
                        await getTask();
                        transaction.Commit();
                    }
                }
                else
                {
                    await getTask();
                }

                await _auditingService.SaveAuditRecordAsync(new DeleteAccountAuditRecord(username: userToBeDeleted.Username,
                    status: DeleteAccountAuditRecord.ActionStatus.Success,
                    action: AuditedDeleteAccountAction.DeleteAccount,
                    adminUsername: requestingUser.Username));

                return new DeleteUserAccountStatus()
                {
                    Success = true,
                    Description = string.Format(CultureInfo.CurrentCulture,
                        Strings.AccountDelete_Success,
                        userToBeDeleted.Username),
                    AccountName = userToBeDeleted.Username
                };
            }
            catch (Exception e)
            {
                QuietLog.LogHandledException(e);
                return new DeleteUserAccountStatus()
                {
                    Success = false,
                    Description = string.Format(CultureInfo.CurrentCulture,
                        Strings.AccountDelete_Fail,
                        userToBeDeleted.Username, e),
                    AccountName = userToBeDeleted.Username
                };
            }
        }
    }
}