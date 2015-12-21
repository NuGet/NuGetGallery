﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public interface IPackageService
    {
        PackageRegistration FindPackageRegistrationById(string id);
        Package FindPackageByIdAndVersion(string id, string version, bool allowPrerelease = true);
        IEnumerable<Package> FindPackagesByOwner(User user, bool includeUnlisted);
        IEnumerable<Package> FindDependentPackages(Package package);

        void UpdateIsLatest(PackageRegistration packageRegistration, bool commitChanges = true);

        /// <summary>
        /// Populate the related database tables to create the specified package for the specified user.
        /// </summary>
        /// <remarks>
        /// This method doesn't upload the package binary to the blob storage. The caller must do it after this call.
        /// </remarks>
        /// <param name="nugetPackage">The package to be created.</param>
        /// <param name="packageStreamMetadata">The package stream's metadata.</param>
        /// <param name="user">The owner of the package</param>
        /// <param name="commitChanges">Specifies whether to commit the changes to database.</param>
        /// <returns>The created package entity.</returns>
        Package CreatePackage(PackageReader nugetPackage, PackageStreamMetadata packageStreamMetadata, User user, bool commitChanges = true);

        void PublishPackage(string id, string version, bool commitChanges = true);
        void PublishPackage(Package package, bool commitChanges = true);

        void MarkPackageUnlisted(Package package, bool commitChanges = true);
        void MarkPackageListed(Package package, bool commitChanges = true);
        void AddDownloadStatistics(PackageStatistics stats);

        PackageOwnerRequest CreatePackageOwnerRequest(PackageRegistration package, User currentOwner, User newOwner);
        ConfirmOwnershipResult ConfirmPackageOwner(PackageRegistration package, User user, string token);
        void AddPackageOwner(PackageRegistration package, User user);
        void RemovePackageOwner(PackageRegistration package, User user);

        void SetLicenseReportVisibility(Package package, bool visible, bool commitChanges = true);
    }
}