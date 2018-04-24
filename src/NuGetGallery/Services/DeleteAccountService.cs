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
            string signature,
            bool unlistOrphanPackages,
            bool commitAsTransaction)
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

            // The deletion of Organization and Organization member accounts is disabled for now.
            if (userToBeDeleted is Organization)
            {
                return new DeleteUserAccountStatus()
                {
                    Success = false,
                    Description = string.Format(CultureInfo.CurrentCulture,
                        Strings.AccountDelete_OrganizationDeleteNotImplemented,
                        userToBeDeleted.Username),
                    AccountName = userToBeDeleted.Username
                };
            }

            try
            {
                // The support requests db and gallery db are different.
                // TransactionScope can be used for doing transaction actions across db on the same server but not on different servers.
                // The below code will clean first the suppport requests and after the gallery data.
                // The order is important in order to allow the admin the oportunity to execute this step again.
                await RemoveSupportRequests(userToBeDeleted);

                if (commitAsTransaction)
                {
                    using (var strategy = new SuspendDbExecutionStrategy())
                    using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
                    {
                        await DeleteGalleryUserAccountImplAsync(userToBeDeleted, userToExecuteTheDelete, signature, unlistOrphanPackages, HardDeleteUser(userToBeDeleted));
                        transaction.Commit();
                    }
                }
                else
                {
                    await DeleteGalleryUserAccountImplAsync(userToBeDeleted, userToExecuteTheDelete, signature, unlistOrphanPackages, HardDeleteUser(userToBeDeleted));
                }
                await _auditingService.SaveAuditRecordAsync(new DeleteAccountAuditRecord(username: userToBeDeleted.Username,
                    status: DeleteAccountAuditRecord.ActionStatus.Success,
                    action: AuditedDeleteAccountAction.DeleteAccount,
                    adminUsername: userToExecuteTheDelete.Username));
                return new DeleteUserAccountStatus()
                {
                    Success = true,
                    Description = string.Format(CultureInfo.CurrentCulture,
                        Strings.AccountDelete_Success,
                        userToBeDeleted.Username),
                    AccountName = userToBeDeleted.Username
                };
            }
            catch(Exception e)
            {
                QuietLog.LogHandledException(e);
                return new DeleteUserAccountStatus()
                {
                    Success = true,
                    Description = string.Format(CultureInfo.CurrentCulture,
                        Strings.AccountDelete_Fail,
                        userToBeDeleted.Username, e),
                    AccountName = userToBeDeleted.Username
                };
            }
        }

        internal bool HardDeleteUser(User userToBeDeleted)
        {
            // If the userToBeDeleted does not have the email confirmed the user will be removed from the database.
            return !userToBeDeleted.Confirmed;
        }

        private async Task DeleteGalleryUserAccountImplAsync(User userToBeDeleted, User admin, string signature, bool unlistOrphanPackages, bool hardDelete)
        {
            var ownedPackages = _packageService.FindPackagesByAnyMatchingOwner(userToBeDeleted, includeUnlisted: true, includeVersions: true).ToList();

            await RemoveOwnership(userToBeDeleted, admin, unlistOrphanPackages, ownedPackages);
            await RemovePackageOwnershipRequests(userToBeDeleted);
            await RemoveMemberships(userToBeDeleted);
            await RemoveReservedNamespaces(userToBeDeleted);
            await RemoveSecurityPolicies(userToBeDeleted);
            await RemoveUserCredentials(userToBeDeleted);
            if (hardDelete)
            {
                await RemoveUser(userToBeDeleted);
            }
            else
            {
                await RemoveUserDataInUserTable(userToBeDeleted);
                await InsertDeleteAccount(userToBeDeleted, admin, signature);
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

        private async Task RemoveOwnership(User user, User admin, bool unlistOrphanPackages, List<Package> packages)
        {
            foreach (var package in packages)
            {
                if (unlistOrphanPackages && _packageService.GetPackageUserAccountOwners(package).Count() <= 1)
                {
                    await _packageService.MarkPackageUnlistedAsync(package, commitChanges: true);
                }
                await _packageOwnershipManagementService.RemovePackageOwnerAsync(package.PackageRegistration, admin, user, commitAsTransaction:false);
            }
        }

        private async Task RemovePackageOwnershipRequests(User user)
        {
            var requests = _packageOwnershipManagementService.GetPackageOwnershipRequests(newOwner: user).ToArray();
            foreach (var request in requests)
            {
                await _packageOwnershipManagementService.DeletePackageOwnershipRequestAsync(request.PackageRegistration, request.NewOwner);
            }
        }

        private async Task RemoveMemberships(User user)
        {
            foreach (var membership in user.Organizations.ToArray())
            {
                var organization = membership.Organization;
                var collaborators = organization.Members.Where(m => !m.IsAdmin).ToList();
                var memberCount = organization.Members.Count();
                user.Organizations.Remove(membership);
                
                if (memberCount < 2)
                {
                    // This is the only member of the organization.
                    // We should delete the entire organization.
                }
                else if (memberCount - 1 == collaborators.Count())
                {
                    // All other members of this organization are collaborators, so we should promote them to administrators.
                    foreach (var collaborator in collaborators)
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
    }
}