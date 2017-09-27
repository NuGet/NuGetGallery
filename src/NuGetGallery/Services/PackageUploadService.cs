// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class PackageUploadService : IPackageUploadService
    {
        private readonly IPackageService _packageService;
        private readonly IReservedNamespaceService _reservedNamespaceService;
        private readonly IValidationService _validationService;

        public PackageUploadService(
            IPackageService packageService,
            IReservedNamespaceService reservedNamespaceService,
            IValidationService validationService)
        {
            _packageService = packageService;
            _reservedNamespaceService = reservedNamespaceService;
            _validationService = validationService;
        }

        public async Task<Package> GeneratePackageAsync(string id, PackageArchiveReader nugetPackage, PackageStreamMetadata packageStreamMetadata, User user)
        {
            var isPushAllowed = _reservedNamespaceService.IsPushAllowed(id, user, out IReadOnlyCollection<ReservedNamespace> userOwnedNamespaces);
            var shouldMarkIdVerified = isPushAllowed && userOwnedNamespaces != null && userOwnedNamespaces.Any();

            var package = await _packageService.CreatePackageAsync(
                nugetPackage,
                packageStreamMetadata,
                user,
                isVerified: shouldMarkIdVerified);

            await _validationService.StartValidationAsync(package);

            if (shouldMarkIdVerified)
            {
                // Add all relevant package registrations to the applicable namespaces
                foreach (var rn in userOwnedNamespaces)
                {
                    _reservedNamespaceService.AddPackageRegistrationToNamespace(
                        rn.Value,
                        package.PackageRegistration);
                }
            }

            return package;
        }
    }
}