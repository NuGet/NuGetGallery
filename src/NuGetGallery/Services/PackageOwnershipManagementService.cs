// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using NuGetGallery.Auditing;
using NuGet.Services.Entities;

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

        public async Task AddPackageOwnerAsync(PackageRegistration packageRegistration, User user, bool commitChanges = true)
        {
            if (packageRegistration == null)
            {
                throw new ArgumentNullException(nameof(packageRegistration));
            }

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (commitChanges)
            {
                using (var strategy = new SuspendDbExecutionStrategy())
                using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
                {
                    await AddPackageOwnerTask(packageRegistration, user, commitChanges);

                    transaction.Commit();
                }
            }
            else
            {
                await AddPackageOwnerTask(packageRegistration, user, commitChanges);
            }

            await _auditingService.SaveAuditRecordAsync(
                new PackageRegistrationAuditRecord(packageRegistration, AuditedPackageRegistrationAction.AddOwner, user.Username));
        }

        private async Task AddPackageOwnerTask(PackageRegistration packageRegistration, User user, bool commitChanges = true)
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
                    await _packageService.UpdatePackageVerifiedStatusAsync(new List<PackageRegistration> { packageRegistration }, isVerified: true, commitChanges: commitChanges);
                }

                userOwnedMatchingNamespacesForId
                    .ToList()
                    .ForEach(mn =>
                        _reservedNamespaceService.AddPackageRegistrationToNamespace(mn.Value, packageRegistration));

                if (commitChanges)
                {
                    // The 'AddPackageRegistrationToNamespace' does not commit its changes, so saving changes for consistency.
                    await _entitiesContext.SaveChangesAsync();
                }
            }

            await _packageService.AddPackageOwnerAsync(packageRegistration, user, commitChanges);

            await DeletePackageOwnershipRequestAsync(packageRegistration, user, commitChanges);
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

        public async Task RemovePackageOwnerAsync(PackageRegistration packageRegistration, User requestingOwner, User ownerToBeRemoved, bool commitChanges = true)
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
                if (commitChanges)
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
                    await RemovePackageOwnerImplAsync(packageRegistration, ownerToBeRemoved, commitChanges: false);
                }

                await _auditingService.SaveAuditRecordAsync(
                    new PackageRegistrationAuditRecord(packageRegistration, AuditedPackageRegistrationAction.RemoveOwner, ownerToBeRemoved.Username));
            }
            else
            {
                throw new InvalidOperationException(string.Format(Strings.RemoveOwner_NotAllowed, requestingOwner.Username, ownerToBeRemoved.Username));
            }
        }

        private async Task RemovePackageOwnerImplAsync(PackageRegistration packageRegistration, User ownerToBeRemoved, bool commitChanges = true)
        {
            // Remove the user from owners list of package registration
            await _packageService.RemovePackageOwnerAsync(packageRegistration, ownerToBeRemoved, commitChanges: false);

            // Remove this package registration from the namespaces owned by this user that are owned by no other package owners
            foreach (var reservedNamespace in packageRegistration.ReservedNamespaces.ToArray())
            {
                if (!packageRegistration.Owners
                    .Any(o => ActionsRequiringPermissions.AddPackageToReservedNamespace
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

            if (commitChanges)
            {
                await _entitiesContext.SaveChangesAsync();
            }
        }

        public async Task DeletePackageOwnershipRequestAsync(PackageRegistration packageRegistration, User newOwner, bool commitChanges = true)
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
                await _packageOwnerRequestService.DeletePackageOwnershipRequest(request, commitChanges);
            }
        }

        private static bool OwnerHasPermissionsToRemove(User requestingOwner, User ownerToBeRemoved, PackageRegistration packageRegistration)
        {
            var reservedNamespaces = packageRegistration.ReservedNamespaces.ToList();
            if (ActionsRequiringPermissions.AddPackageToReservedNamespace
                .CheckPermissionsOnBehalfOfAnyAccount(ownerToBeRemoved, reservedNamespaces) == PermissionsCheckResult.Allowed)
            {
                // If the owner to be removed owns a reserved namespace that applies to this package,
                // the requesting user must own a reserved namespace that applies to this package or be a site admin.
                return ActionsRequiringPermissions.RemovePackageFromReservedNamespace
                    .CheckPermissionsOnBehalfOfAnyAccount(requestingOwner, reservedNamespaces) == PermissionsCheckResult.Allowed;
            }

            // If the owner to be removed does not own any reserved namespaces that apply to this package, they can be removed by anyone.
            return true;
        }
    }
}