// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGetGallery.Authentication;
using NuGetGallery.Areas.Admin.ViewModels;
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

        public DeleteAccountService(IEntityRepository<AccountDelete> accountDeleteRepository,
                                    IEntityRepository<User> userRepository,
                                    IEntitiesContext entitiesContext,
                                    IPackageService packageService,
                                    IPackageOwnershipManagementService packageOwnershipManagementService,
                                    IReservedNamespaceService reservedNamespaceService,
                                    ISecurityPolicyService securityPolicyService,
                                    AuthenticationService authService
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
        }

        /// <summary>
        /// Will clean-up the data related with an user account.
        /// The result will be:
        /// 1. The user will be removed as owner from its owned packages.
        /// 2. Any of the packages that become orphaned as its result will be unlisted if the unlistOrphanPackages is set to true.
        /// 3. Any owned namespaces will be released.
        /// 4. The user credentials will be cleaned.
        /// 5. The user data will be cleaned.
        /// </summary>
        /// <param name="userToBeDeleted">The user to be deleted.</param>
        /// <param name="admin">The admin that will perform the delete action.</param>
        /// <param name="signature">The admin signature.</param>
        /// <param name="unlistOrphanPackages">If the orphaned packages will unlisted.</param>
        /// <param name="commitAsTransaction">If the data will be persisted as a transaction.</param>
        /// <returns></returns>
        public async Task<DeleteUserAccountStatus> DeleteGalleryUserAccountAsync(User userToBeDeleted, User admin, string signature, bool unlistOrphanPackages, bool commitAsTransaction)
        {
            if (userToBeDeleted == null)
            {
                throw new ArgumentNullException(nameof(userToBeDeleted));
            }
            if (admin == null)
            {
                throw new ArgumentNullException(nameof(admin));
            }
            if(userToBeDeleted.IsDeleted)
            {
                return new DeleteUserAccountStatus()
                {
                    Success = false,
                    Description = string.Format(Strings.AccountDelete_AccountAlreadyDeleted, userToBeDeleted.Username),
                    AccountName = userToBeDeleted.Username
                };
            }
            try
            {
                if (commitAsTransaction)
                {
                    using (var strategy = new SuspendDbExecutionStrategy())
                    using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
                    {
                        await DeleteGalleryUserAccountImplAsync(userToBeDeleted, admin, signature, unlistOrphanPackages);
                        transaction.Commit();
                    }
                }
                else
                {
                    await DeleteGalleryUserAccountImplAsync(userToBeDeleted, admin, signature, unlistOrphanPackages);
                }
                return new DeleteUserAccountStatus()
                {
                    Success = true,
                    Description = string.Format(Strings.AccountDelete_Success, userToBeDeleted.Username),
                    AccountName = userToBeDeleted.Username
                };
            }
            catch(Exception e)
            {
                return new DeleteUserAccountStatus()
                {
                    Success = true,
                    Description = string.Format(Strings.AccountDelete_Fail, userToBeDeleted.Username, e),
                    AccountName = userToBeDeleted.Username
                };
            }
        }

        private async Task DeleteGalleryUserAccountImplAsync(User useToBeDeleted, User admin, string signature, bool unlistOrphanPackages)
        {
            var ownedPackages = _packageService.FindPackagesByOwner(useToBeDeleted, includeUnlisted: true).ToList();

            await RemoveOwnership(useToBeDeleted, admin, unlistOrphanPackages, ownedPackages);
            await RemoveReservedNamespaces(useToBeDeleted);
            await RemoveSecurityPolicies(useToBeDeleted);
            await RemoveUserCredentials(useToBeDeleted);
            await RemoveUserDataInUserTable(useToBeDeleted);
            await InsertDeleteAccount(useToBeDeleted, admin, signature);
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

        private async Task RemoveOwnership(User user, User admin, bool unsignOrphanPackages, List<Package> packages)
        {
            foreach (var package in packages)
            {
                if (unsignOrphanPackages && package.PackageRegistration.Owners.Count() <= 1)
                {
                    await _packageService.MarkPackageUnlistedAsync(package, commitChanges: true);
                }
                await _packageOwnershipManagementService.RemovePackageOwnerAsync(package.PackageRegistration, admin, user, commitAsTransaction:false);
            }
        }

        private async Task RemoveUserDataInUserTable(User user)
        {
            user.SetAccountAsDeleted();
            await _userRepository.CommitChangesAsync();
        }
    }
}