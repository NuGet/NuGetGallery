// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication;
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
        private readonly ISupportRequestService _supportRequestService;
        private readonly IAuditingService _auditingService;

        public DeleteAccountService(IEntityRepository<AccountDelete> accountDeleteRepository,
                                    IEntityRepository<User> userRepository,
                                    IEntitiesContext entitiesContext,
                                    IPackageService packageService,
                                    IPackageOwnershipManagementService packageOwnershipManagementService,
                                    IReservedNamespaceService reservedNamespaceService,
                                    ISecurityPolicyService securityPolicyService,
                                    AuthenticationService authService,
                                    ISupportRequestService supportRequestService,
                                    IAuditingService auditingService
            )
        {
            _accountDeleteRepository = accountDeleteRepository ?? throw new ArgumentNullException(nameof(accountDeleteRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _packageOwnershipManagementService = packageOwnershipManagementService ?? throw new ArgumentNullException(nameof(packageOwnershipManagementService));
            _reservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));
            _securityPolicyService = securityPolicyService ?? throw new ArgumentNullException(nameof(securityPolicyService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _supportRequestService = supportRequestService ?? throw new ArgumentNullException(nameof(supportRequestService));
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
        }

        public async Task<DeleteUserAccountStatus> DeleteGalleryUserAccountAsync(User userToBeDeleted,
            User userToExecuteTheDelete,
            bool commitAsTransaction,
            AccountDeletionOrphanPackagePolicy orphanPackagePolicy = AccountDeletionOrphanPackagePolicy.DoNotAllowOrphans,
            string signature = null)
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

            // The deletion of members of organizations is disabled for now.
            if (userToBeDeleted.Organizations.Any())
            {
                return new DeleteUserAccountStatus()
                {
                    Success = false,
                    Description = string.Format(CultureInfo.CurrentCulture,
                        Strings.AccountDelete_OrganizationMemberDeleteNotImplemented,
                        userToBeDeleted.Username),
                    AccountName = userToBeDeleted.Username
                };
            }
            
            return await RunAccountDeletionTask(
                () => DeleteGalleryUserAccountImplAsync(
                    userToBeDeleted, 
                    userToExecuteTheDelete,
                    orphanPackagePolicy,
                    signature),
                userToBeDeleted,
                userToExecuteTheDelete,
                commitAsTransaction);
        }

        private async Task DeleteGalleryUserAccountImplAsync(User userToBeDeleted, User userToExecuteTheDelete, AccountDeletionOrphanPackagePolicy orphanPackagePolicy, string signature = null)
        {
            await RemoveReservedNamespaces(userToBeDeleted);
            await RemovePackageOwnership(userToBeDeleted, userToExecuteTheDelete, orphanPackagePolicy);
            await RemoveSecurityPolicies(userToBeDeleted);
            await RemoveUserCredentials(userToBeDeleted);
            await RemovePackageOwnershipRequests(userToBeDeleted);

            var organizationToBeDeleted = userToBeDeleted as Organization;
            if (organizationToBeDeleted != null)
            {
                await RemoveMemberships(organizationToBeDeleted);
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
                    userToExecuteTheDelete, 
                    signature ?? userToExecuteTheDelete.Username);
            }
        }

        private async Task InsertDeleteAccount(User user, User admin, string signature)
        {
            var accountDelete = new AccountDelete
            {
                DeletedOn = DateTime.UtcNow,
                DeletedAccountKey = user.Key,
                DeletedByKey = admin.Key,
                Signature = signature
            };
            _accountDeleteRepository.InsertOnCommit(accountDelete);
            await _accountDeleteRepository.CommitChangesAsync();
        }

        private async Task RemoveUserCredentials(User user)
        {
            var copyOfUserCred = user.Credentials.ToArray();
            foreach (var uc in copyOfUserCred)
            {
                await _authService.RemoveCredential(user, uc);
            }
        }

        private async Task RemoveSecurityPolicies(User user)
        {
            var copyOfUserPolicies = user.SecurityPolicies.ToArray();
            foreach (var usp in copyOfUserPolicies)
            {
                await _securityPolicyService.UnsubscribeAsync(user, usp.Subscription);
            }
        }

        private async Task RemoveReservedNamespaces(User user)
        {
            var copyOfUserNS = user.ReservedNamespaces.ToArray();
            foreach (var rn in copyOfUserNS)
            {
                await _reservedNamespaceService.DeleteOwnerFromReservedNamespaceAsync(rn.Value, user.Username, commitAsTransaction:false);
            }
        }

        private async Task RemovePackageOwnership(User user, User requestingUser, AccountDeletionOrphanPackagePolicy orphanPackagePolicy)
        {
            foreach (var package in GetPackagesOwnedByUser(user))
            {
                var owners = user is Organization ? package.PackageRegistration.Owners : _packageService.GetPackageUserAccountOwners(package);
                if (owners.Count() <= 1)
                {
                    // Package will be orphaned by removing ownership.
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

        private bool WillPackageBeOrphaned(User user, Package package)
        {
            var owners = user is Organization ? package.PackageRegistration.Owners : _packageService.GetPackageUserAccountOwners(package);
            return owners.Count() <= 1;
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

        private async Task RemoveMemberships(Organization organization)
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
            await _supportRequestService.DeleteSupportRequestsAsync(user.Username);
        }

        private async Task RemoveUser(User user)
        {
            _userRepository.DeleteOnCommit(user);
            await _userRepository.CommitChangesAsync();
        }

        private async Task RemoveUser(Organization organization)
        {
            _entitiesContext.DeleteOnCommit(organization);
            await _entitiesContext.SaveChangesAsync();
        }

        private async Task<DeleteUserAccountStatus> RunAccountDeletionTask(Func<Task> getTask, User userToBeDeleted, User requestingUser, bool commitAsTransaction)
        {
            try
            {
                // The support requests DB and gallery DB are different.
                // TransactionScope can be used for doing transaction actions across db on the same server but not on different servers.
                // The below code will clean the suppport requests before the gallery data.
                // The order is important in order to allow the admin the opportunity to execute this step again.
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