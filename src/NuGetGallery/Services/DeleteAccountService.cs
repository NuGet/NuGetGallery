// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication;
using NuGetGallery.Security;

namespace NuGetGallery
{
    public class DeleteAccountService : IDeleteAccountService
    {
        private readonly IEntityRepository<Package> _packageRepository;
        private readonly IEntityRepository<AccountDelete> _accountDeleteRepository;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IPackageService _packageService;
        private readonly IPackageOwnershipManagementService _packageOwnershipManagementService;
        private readonly IReservedNamespaceService _reservedNamespaceService;
        private readonly IUserService _userService;
        private readonly ISecurityPolicyService _securityPolicyService;
        private readonly AuthenticationService _authService;
        private readonly IAuditingService _auditingService;
        private readonly IEntityRepository<User> _userRepository;

        public DeleteAccountService(IEntityRepository<Package> packageRepository,
                                    IEntityRepository<AccountDelete> accountDeleteRepository,
                                    IEntityRepository<User> userRepository,
                                    IEntitiesContext entitiesContext,
                                    IPackageService packageService,
                                    IPackageOwnershipManagementService packageOwnershipManagementService,
                                    IReservedNamespaceService reservedNamespaceService,
                                    IUserService userService,
                                    ISecurityPolicyService securityPolicyService,
                                    AuthenticationService authService,
                                    IAuditingService auditingService
            )
        {
            _packageRepository = packageRepository ?? throw new ArgumentNullException(nameof(packageRepository));
            _accountDeleteRepository = accountDeleteRepository ?? throw new ArgumentNullException(nameof(accountDeleteRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _packageOwnershipManagementService = packageOwnershipManagementService ?? throw new ArgumentNullException(nameof(packageOwnershipManagementService));
            _reservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _securityPolicyService = securityPolicyService ?? throw new ArgumentNullException(nameof(securityPolicyService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        /// <summary>
        /// Will clean-up the data related with an user account.
        /// The result will be:
        /// 1. The user will be removed as owner from his owned packages.
        /// 2. Any of the packages that become orphaned as this result will be unlisted if the unlistOrphanPackages is set to true.
        /// 3. Any owned namespaces will be released.
        /// 4. The user credentials will be cleaned.
        /// 5. The user data will be cleaned.
        /// </summary>
        /// <param name="userName">The NuGet user name</param>
        /// <param name="unlistOrphanPackages">True if the orphan packages will be unlisted.</param>
        /// <returns>A list with information regarding the status of each clean-up operation.</returns>
        public async Task<Tuple<bool, List<string>>> DeleteGalleryUserAccountAsync(User useToBeDeleted, User admin, string signature, bool unlistOrphanPackages)
        {
            if (useToBeDeleted == null)
            {
                throw new ArgumentNullException(nameof(useToBeDeleted));
            }
            if (admin == null)
            {
                throw new ArgumentNullException(nameof(admin));
            }

            var ownedPackages = _packageService.FindPackagesByOwner(useToBeDeleted, includeUnlisted: true).ToList();
            List<string> executionDetails = new List<string>();
            bool result = true;

            result = (await RemoveOwnership(useToBeDeleted, admin, unlistOrphanPackages, ownedPackages, executionDetails)) ?
                            (await RemoveReservedNamespaces(useToBeDeleted, executionDetails) ?
                                (await RemoveSecurityPolicies(useToBeDeleted, executionDetails) ?
                                    (await RemoveUserCredentials(useToBeDeleted, executionDetails) ?
                                        (await RemoveUserDataInUserTable(useToBeDeleted, executionDetails) ?
                                            await InsertDeleteAccount(useToBeDeleted, admin, signature, executionDetails)
                                         : false)
                                     : false)
                                 : false)
                             : false)
                         : false;
            return new Tuple<bool, List<string>>(result, executionDetails);
        }

        private async Task<bool> InsertDeleteAccount(User user, User admin, string signature, List<string> executionDetails)
        {
            try
            {
                var accountDelete = new AccountDelete
                {
                    DeletedOn = DateTime.UtcNow,
                    DeletedAccount = user,
                    DeletedBy = admin,
                    Signature = signature
                };
                _accountDeleteRepository.InsertOnCommit(accountDelete);
                await _accountDeleteRepository.CommitChangesAsync();
                executionDetails.Add($"{nameof(InsertDeleteAccount)}: Succeeded");
                return true;
            }
            catch (Exception e)
            {
                executionDetails.Add($"{nameof(InsertDeleteAccount)}: {e.ToString()}");
                return false;
            }
        }

        private async Task<bool> RemoveUserCredentials(User user, List<string> executionDetails)
        {
            try
            {
                var copyOfUserCred = user.Credentials.ToArray();

                foreach (var uc in copyOfUserCred)
                {
                    await _authService.RemoveCredential(user, uc);
                }
                executionDetails.Add($"{nameof(RemoveUserCredentials)}: Succeeded");
                return true;
            }
            catch (Exception e)
            {
                executionDetails.Add($"{nameof(RemoveUserCredentials)}: {e.ToString()}");
                return false;
            }
        }

        private async Task<bool> RemoveSecurityPolicies(User user, List<string> executionDetails)
        {
            try
            {
                var copyOfUserPolicies = user.SecurityPolicies.ToArray();
                foreach (var usp in copyOfUserPolicies)
                {
                    await _securityPolicyService.UnsubscribeAsync(user, usp.Subscription);
                }
                executionDetails.Add($"{nameof(RemoveSecurityPolicies)}: Succeeded");
                return true;
            }
            catch (Exception e)
            {
                executionDetails.Add($"{nameof(RemoveSecurityPolicies)}: {e.ToString()}");
                return false;
            }
        }

        private async Task<bool> RemoveReservedNamespaces(User user, List<string> executionDetails)
        {
            try
            {
                var copyOfUserNS = user.ReservedNamespaces.ToArray();
                foreach (var rn in copyOfUserNS)
                {
                    await _reservedNamespaceService.DeleteOwnerFromReservedNamespaceAsync(rn.Value, user.Username);
                }
                executionDetails.Add($"{nameof(RemoveReservedNamespaces)}: Succeeded");
                return true;
            }
            catch (Exception e)
            {
                executionDetails.Add($"{nameof(RemoveReservedNamespaces)}: {e.ToString()}");
                return false;
            }
        }

        private async Task<bool> RemoveOwnership(User user, User admin, bool unsignOrphanPackages, List<Package> packages, List<string> executionDetails)
        {
            try
            {
                foreach (var package in packages)
                {
                    if (unsignOrphanPackages && package.PackageRegistration.Owners.Count() <= 1)
                    {
                        await _packageService.MarkPackageUnlistedAsync(package, true);
                    }
                    await _packageOwnershipManagementService.RemovePackageOwnerAsync(package.PackageRegistration, admin, user);
                }
                executionDetails.Add($"{nameof(RemoveOwnership)}: Succeeded");
                return true;
            }
            catch (Exception e)
            {
                executionDetails.Add($"{nameof(RemoveOwnership)}: {e.ToString()}");
                return false;
            }
        }

        private async Task<bool> RemoveUserDataInUserTable(User user, List<string> executionDetails)
        {
            try
            {
                user.SetAccountAsDeleted();
                await _userRepository.CommitChangesAsync();
                executionDetails.Add($"{nameof(RemoveUserDataInUserTable)}: Succeeded");
                return true;
            }
            catch (Exception e)
            {
                executionDetails.Add($"{nameof(RemoveUserDataInUserTable)}: {e.ToString()}");
                return false;
            }
        }
    }
}