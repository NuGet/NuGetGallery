// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using NuGet.Packaging;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class PackageUploadService: IPackageUploadService
    {
        private readonly IEntitiesContext _entitiesContext;
        private readonly IPackageService _packageService;
        private readonly IReservedNamespaceService _reservedNamespaceService;

        public PackageUploadService(
            IEntitiesContext entitiesContext,
            IPackageService packageService,
            IReservedNamespaceService reservedNamespaceService)
        {
            _entitiesContext = entitiesContext;
            _packageService = packageService;
            _reservedNamespaceService = reservedNamespaceService;
        }

        public async Task<Package> CreatePackageAsync(string id, PackageArchiveReader nugetPackage, PackageStreamMetadata packageStreamMetadata, User user, bool commitChanges)
        {
            var isPushAllowed = _reservedNamespaceService.IsPushAllowed(id, user, out IReadOnlyCollection<ReservedNamespace> userOwnedNamespaces);

            var shouldMarkIdVerified = userOwnedNamespaces != null && userOwnedNamespaces.Any();
            var package = await _packageService.CreatePackageAsync(
                nugetPackage,
                packageStreamMetadata,
                user,
                isVerified: shouldMarkIdVerified,
                commitChanges: false);

            if (shouldMarkIdVerified)
            {
                // Add all relevant package registrations to the applicable namespaces
                await Task.WhenAll(userOwnedNamespaces
                    .Select(rn => _reservedNamespaceService.AddPackageRegistrationToNamespaceAsync(rn.Value, package.PackageRegistration, commitChanges: false)));
            }

            return package;
        }
    }
}