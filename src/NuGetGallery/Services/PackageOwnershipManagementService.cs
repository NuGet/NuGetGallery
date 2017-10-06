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
            using (var strategy = new SuspendDbExecutionStrategy())
            using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
            {
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

                await _packageService.AddPackageOwnerAsync(packageRegistration, user);

                var request = _packageOwnerRequestService.GetPackageOwnershipRequests(package: packageRegistration, newOwner: user).FirstOrDefault();
                if (request != null)
                {
                    await _packageOwnerRequestService.DeletePackageOwnershipRequest(request);
                }

                transaction.Commit();
            }

            await _auditingService.SaveAuditRecordAsync(
                new PackageRegistrationAuditRecord(packageRegistration, AuditedPackageRegistrationAction.AddOwner, user.Username));
        }

        public async Task RemovePackageOwnerAsync(PackageRegistration packageRegistration, User user)
        {
            // Called when removing an owner and when rejecting the request for ownership.
            // If rejecting the ownership, simply remove the entry from ownership request and return
            var request = _packageOwnerRequestService.GetPackageOwnershipRequests(package: packageRegistration, newOwner: user).FirstOrDefault();
            if (request != null)
            {
                await _packageOwnerRequestService.DeletePackageOwnershipRequest(request);
                return;
            }

            using (var strategy = new SuspendDbExecutionStrategy())
            using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
            {
                // 1. Remove this package registration from the namespaces owned by this user, if he is the only package owner in the set of matching namespaces
                // 2. Remove the IsVerified flag from package registration, if all the matching namespaces where owned by this user alone(no other owner of package owns a matching namespace for this PR)
                var allMatchingNamespacesForRegistration = packageRegistration.ReservedNamespaces.ToList();
                if (allMatchingNamespacesForRegistration.Any())
                {
                    var allPackageOwners = packageRegistration.Owners;
                    var matchingNamespacesOwnedByUser = allMatchingNamespacesForRegistration
                        .Where(rn => rn.Owners.Any(o => o == user));
                    var namespacesToModify = matchingNamespacesOwnedByUser
                        .Where(rn => rn.Owners.Intersect(allPackageOwners).Count() == 1)
                        .ToList();

                    if (namespacesToModify.Any())
                    {
                        // The package will lose its 'IsVerified' flag if the user is the only package owner who owns all the namespaces that match this registration
                        var shouldModifyIsVerified = allMatchingNamespacesForRegistration.Count() == namespacesToModify.Count();
                        if (shouldModifyIsVerified && packageRegistration.IsVerified)
                        {
                            await _packageService.UpdatePackageVerifiedStatusAsync(new List<PackageRegistration> { packageRegistration }, isVerified: false);
                        }

                        namespacesToModify
                            .ForEach(rn => _reservedNamespaceService.RemovePackageRegistrationFromNamespace(rn.Value, packageRegistration));

                        await _entitiesContext.SaveChangesAsync();
                    }
                }

                // Remove the user from owners list of package registration
                await _packageService.RemovePackageOwnerAsync(packageRegistration, user);

                transaction.Commit();
            }

            await _auditingService.SaveAuditRecordAsync(
                new PackageRegistrationAuditRecord(packageRegistration, AuditedPackageRegistrationAction.RemoveOwner, user.Username));
        }

    }
}