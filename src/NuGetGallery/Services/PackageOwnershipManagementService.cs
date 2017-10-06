// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class PackageOwnershipManagementService : IPackageOwnershipManagementService
    {
        private readonly IPackageService _packageService;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IReservedNamespaceService _reservedNamespaceService;

        public PackageOwnershipManagementService(
            IPackageService packageService,
            IEntitiesContext entitiesContext,
            IReservedNamespaceService reservedNamespaceService)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _reservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));
        }

        public async Task AddPackageOwnerAsync(PackageRegistration packageRegistration, User user)
        {
            using (var strategy = new SuspendDbExecutionStrategy())
            using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
            {
                await _packageService.AddPackageOwnerAsync(packageRegistration, user);

                var userOwnedMatchingNamespacesForId = user
                    .ReservedNamespaces
                    .Where(rn => packageRegistration.Id.StartsWith(rn.Value, StringComparison.OrdinalIgnoreCase));

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

                    await _entitiesContext.SaveChangesAsync();
                }

                transaction.Commit();
            }
        }

        public async Task RemovePackageOwnerAsync(PackageRegistration packageRegistration, User user)
        {
            using (var strategy = new SuspendDbExecutionStrategy())
            using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
            {
                await _packageService.RemovePackageOwnerAsync(packageRegistration, user);

                // 1. Remove this package registration from the namespaces owned by this user, if he is the only package owner in the set of matching namespaces
                // 2. Remove the IsVerified flag from package registration, if all the matching namespaces where owned by this user alone(no other owner of package owns a matching namespace for this PR)
                var allMatchingNamespaces = packageRegistration.ReservedNamespaces.ToList();
                var allPackageOwners = packageRegistration.Owners;
                var matchingNamespacesOwnedByUser = allMatchingNamespaces
                    .Where(rn => rn.Owners.Any(o => o == user));
                var namespacesToModify = matchingNamespacesOwnedByUser
                    .Where(rn => rn.Owners.Intersect(allPackageOwners).Count() == 1)
                    .ToList();

                // The package will lose its 'IsVerified' flag if the user is the only package owner who owns all the namespaces that match this registration
                var shouldModifyIsVerified = allMatchingNamespaces.Any()
                    && allMatchingNamespaces.Count() == namespacesToModify.Count();
                if (shouldModifyIsVerified && packageRegistration.IsVerified)
                {
                    await _packageService.UpdatePackageVerifiedStatusAsync(new List<PackageRegistration> { packageRegistration }, isVerified: false);
                }

                namespacesToModify
                    .ForEach(rn => _reservedNamespaceService.RemovePackageRegistrationFromNamespace(rn.Value, packageRegistration));

                await _entitiesContext.SaveChangesAsync();

                transaction.Commit();
            }
        }

    }
}