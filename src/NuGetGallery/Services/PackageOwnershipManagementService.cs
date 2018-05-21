// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using NuGetGallery.Auditing;

namespace NuGetGallery
{
    public class PackageOwnershipManagementService : IPackageOwnershipManagementService
    {
        private readonly IPackageService _packageService;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IReservedNamespaceService _reservedNamespaceService;
        private readonly IPackageOwnerRequestService _packageOwnerRequestService;
        private readonly IAuditingService _auditingService;

        public PackageOwnershipManagementService(
            IEntitiesContext entitiesContext,
            IPackageService packageService,
            IReservedNamespaceService reservedNamespaceService,
            IPackageOwnerRequestService packageOwnerRequestService,
            IAuditingService auditingService)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _reservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));
            _packageOwnerRequestService = packageOwnerRequestService ?? throw new ArgumentNullException(nameof(packageOwnerRequestService));
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
        }

        public async Task AddPackageOwnerAsync(PackageRegistration packageRegistration, User user)
        {
            if (packageRegistration == null)
            {
                throw new ArgumentNullException(nameof(packageRegistration));
            }

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            using (var strategy = new SuspendDbExecutionStrategy())
            using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
            {
                Func<ReservedNamespace, bool> predicate =
                    reservedNamespace => reservedNamespace.IsPrefix
                        ? packageRegistration.Id.StartsWith(reservedNamespace.Value, StringComparison.OrdinalIgnoreCase)
                        : packageRegistration.Id.Equals(reservedNamespace.Value, StringComparison.OrdinalIgnoreCase);

                var userOwnedMatchingNamespacesForId = user
                    .ReservedNamespaces
                    .Where(predicate);

                if (userOwnedMatchingNamespacesForId.Any())
                {
                    if (!packageRegistration.IsVerified)
                    {
                        await _packageService.UpdatePackageVerifiedStatusAsync(new List<PackageRegistration> { packageRegistration }, isVerified: true);
                    }

                    userOwnedMatchingNamespacesForId
                        .ToList()
                        .ForEach(mn =>
                            _reservedNamespaceService.AddPackageRegistrationToNamespace(mn.Value, packageRegistration));

                    // The 'AddPackageRegistrationToNamespace' does not commit its changes, so saving changes for consistency.
                    await _entitiesContext.SaveChangesAsync();
                }

                await _packageService.AddPackageOwnerAsync(packageRegistration, user);

                await DeletePackageOwnershipRequestAsync(packageRegistration, user);

                transaction.Commit();
            }

            await _auditingService.SaveAuditRecordAsync(
                new PackageRegistrationAuditRecord(packageRegistration, AuditedPackageRegistrationAction.AddOwner, user.Username));
        }

        public async Task<PackageOwnerRequest> AddPackageOwnershipRequestAsync(PackageRegistration packageRegistration, User requestingOwner, User newOwner)
        {
            return await _packageOwnerRequestService.AddPackageOwnershipRequest(packageRegistration, requestingOwner, newOwner);
        }

        public PackageOwnerRequest GetPackageOwnershipRequest(PackageRegistration package, User pendingOwner, string token)
        {
            return _packageOwnerRequestService.GetPackageOwnershipRequest(package, pendingOwner, token);
        }

        public IEnumerable<PackageOwnerRequest> GetPackageOwnershipRequests(PackageRegistration package = null, User requestingOwner = null, User newOwner = null)
        {
            return _packageOwnerRequestService.GetPackageOwnershipRequests(package, requestingOwner, newOwner);
        }

        public async Task RemovePackageOwnerAsync(PackageRegistration packageRegistration, User requestingOwner, User ownerToBeRemoved, bool commitAsTransaction = true)
        {
            if (packageRegistration == null)
            {
                throw new ArgumentNullException(nameof(packageRegistration));
            }

            if (requestingOwner == null)
            {
                throw new ArgumentNullException(nameof(requestingOwner));
            }

            if (ownerToBeRemoved == null)
            {
                throw new ArgumentNullException(nameof(ownerToBeRemoved));
            }

            if (OwnerHasPermissionsToRemove(requestingOwner, ownerToBeRemoved, packageRegistration))
            {
                if (commitAsTransaction)
                {
                    using (var strategy = new SuspendDbExecutionStrategy())
                    using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
                    {
                        await RemovePackageOwnerImplAsync(packageRegistration, ownerToBeRemoved);
                        transaction.Commit();
                    }
                }
                else
                {
                    await RemovePackageOwnerImplAsync(packageRegistration, ownerToBeRemoved);
                }

                await _auditingService.SaveAuditRecordAsync(
                    new PackageRegistrationAuditRecord(packageRegistration, AuditedPackageRegistrationAction.RemoveOwner, ownerToBeRemoved.Username));
            }
            else
            {
                throw new InvalidOperationException(string.Format(Strings.RemoveOwner_NotAllowed, requestingOwner.Username, ownerToBeRemoved.Username));
            }
        }

        private async Task RemovePackageOwnerImplAsync(PackageRegistration packageRegistration, User ownerToBeRemoved)
        {
            // Remove the user from owners list of package registration
            await _packageService.RemovePackageOwnerAsync(packageRegistration, ownerToBeRemoved, commitChanges: false);

            // Remove this package registration from the namespaces owned by this user that are owned by no other package owners
            foreach (var reservedNamespace in packageRegistration.ReservedNamespaces.ToArray())
            {
                if (!packageRegistration.Owners
                    .Any(o => ActionsRequiringPermissions.ShowPackageAsVerifiedByReservedNamespace
                        .CheckPermissionsOnBehalfOfAnyAccount(o, reservedNamespace) == PermissionsCheckResult.Allowed))
                {
                    _reservedNamespaceService.RemovePackageRegistrationFromNamespace(reservedNamespace, packageRegistration);
                }
            }

            // Remove the IsVerified flag from package registration if all the matching namespaces are owned by this user alone (no other package owner owns a matching namespace for this PR)
            if (packageRegistration.IsVerified && !packageRegistration.ReservedNamespaces.Any())
            {
                await _packageService.UpdatePackageVerifiedStatusAsync(new List<PackageRegistration> { packageRegistration }, isVerified: false, commitChanges: false);
            }

            await _entitiesContext.SaveChangesAsync();
        }

        public async Task DeletePackageOwnershipRequestAsync(PackageRegistration packageRegistration, User newOwner)
        {
            if (packageRegistration == null)
            {
                throw new ArgumentNullException(nameof(packageRegistration));
            }

            if (newOwner == null)
            {
                throw new ArgumentNullException(nameof(newOwner));
            }

            var request = _packageOwnerRequestService.GetPackageOwnershipRequests(package: packageRegistration, newOwner: newOwner).FirstOrDefault();
            if (request != null)
            {
                await _packageOwnerRequestService.DeletePackageOwnershipRequest(request);
            }
        }

        // The requesting owner can remove other owner only if 
        // 1. Is an admin.
        // 2. Owns a namespace.
        // 3. Removing the other owner would not remove the package from all of its reserved namespaces
        private static bool OwnerHasPermissionsToRemove(User requestingOwner, User ownerToBeRemoved, PackageRegistration packageRegistration)
        {
            var reservedNamespaces = packageRegistration.ReservedNamespaces.ToList();
            
            if (ActionsRequiringPermissions.RemovePackageFromReservedNamespace
                .CheckPermissionsOnBehalfOfAnyAccount(requestingOwner, reservedNamespaces) == PermissionsCheckResult.Allowed)
            {
                return true;
            }

            var packageOwnersThatOwnAReservedNamespaceThatThePackageIsAMemberOf =
                packageRegistration.Owners.Count(
                    o => ActionsRequiringPermissions.ShowPackageAsVerifiedByReservedNamespace
                        .CheckPermissionsOnBehalfOfAnyAccount(o, reservedNamespaces) != PermissionsCheckResult.Allowed);

            // If multiple package owners own a reserved namespace that the package is a member of, then removing an owner will not remove the package from all of its reserved namespaces.
            // If no package owners own a reserved namespace that the package is a member of, then the package is not in a reserved namespace and the owner can be removed.
            return packageOwnersThatOwnAReservedNamespaceThatThePackageIsAMemberOf != 1;
        }
    }
}