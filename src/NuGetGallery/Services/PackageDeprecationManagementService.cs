// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGet.Versioning;

namespace NuGetGallery
{
    public class PackageDeprecationManagementService : IPackageDeprecationManagementService
    {
        private readonly IPackageService _packageService;
        private readonly IFeatureFlagService _featureFlagService;
        private readonly IPackageDeprecationService _deprecationService;

        public PackageDeprecationManagementService(
            IPackageService packageService,
            IFeatureFlagService featureFlagService,
            IPackageDeprecationService deprecationService)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _deprecationService = deprecationService ?? throw new ArgumentNullException(nameof(deprecationService));
        }

        public IReadOnlyCollection<string> GetPossibleAlternatePackageVersions(string id)
        {
            return _packageService.FindPackagesById(id)
                .Where(p => p.PackageStatusKey == PackageStatus.Available && p.Listed)
                .Select(p => NuGetVersion.Parse(p.Version))
                .OrderByDescending(v => v)
                .Select(v => v.ToNormalizedString())
                .ToList();
        }

        public async Task<UpdateDeprecationError> UpdateDeprecation(
            User currentUser,
            string id, 
            IEnumerable<string> versions, 
            bool isLegacy = false, 
            bool hasCriticalBugs = false, 
            bool isOther = false, 
            string alternatePackageId = null, 
            string alternatePackageVersion = null, 
            string message = null)
        {
            var status = PackageDeprecationStatus.NotDeprecated;

            if (isLegacy)
            {
                status |= PackageDeprecationStatus.Legacy;
            }

            if (hasCriticalBugs)
            {
                status |= PackageDeprecationStatus.CriticalBugs;
            }

            var customMessage = message;
            if (isOther)
            {
                if (string.IsNullOrWhiteSpace(customMessage))
                {
                    return new UpdateDeprecationError(
                        HttpStatusCode.BadRequest, Strings.DeprecatePackage_CustomMessageRequired);
                }

                status |= PackageDeprecationStatus.Other;
            }

            if (customMessage != null)
            {
                if (customMessage.Length > PackageDeprecation.MaxCustomMessageLength)
                {
                    return new UpdateDeprecationError(
                        HttpStatusCode.BadRequest,
                        string.Format(Strings.DeprecatePackage_CustomMessageTooLong, PackageDeprecation.MaxCustomMessageLength));
                }
            }

            if (versions == null || !versions.Any())
            {
                return new UpdateDeprecationError(
                    HttpStatusCode.BadRequest, Strings.DeprecatePackage_NoVersions);
            }

            var packages = _packageService.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships);
            var registration = packages.FirstOrDefault()?.PackageRegistration;
            if (registration == null)
            {
                // This should only happen if someone hacks the form or if the package is deleted while the user is filling out the form.
                return new UpdateDeprecationError(
                    HttpStatusCode.NotFound,
                    string.Format(Strings.DeprecatePackage_MissingRegistration, id));
            }

            if (!_featureFlagService.IsManageDeprecationEnabled(currentUser, registration))
            {
                return new UpdateDeprecationError(HttpStatusCode.Forbidden, Strings.DeprecatePackage_Forbidden);
            }

            if (ActionsRequiringPermissions.DeprecatePackage.CheckPermissionsOnBehalfOfAnyAccount(currentUser, registration) != PermissionsCheckResult.Allowed)
            {
                return new UpdateDeprecationError(HttpStatusCode.Forbidden, Strings.DeprecatePackage_Forbidden);
            }

            if (registration.IsLocked)
            {
                return new UpdateDeprecationError(
                    HttpStatusCode.Forbidden,
                    string.Format(Strings.DeprecatePackage_Locked, id));
            }

            PackageRegistration alternatePackageRegistration = null;
            Package alternatePackage = null;
            if (!string.IsNullOrWhiteSpace(alternatePackageId))
            {
                if (!string.IsNullOrWhiteSpace(alternatePackageVersion))
                {
                    alternatePackage = _packageService.FindPackageByIdAndVersionStrict(alternatePackageId, alternatePackageVersion);
                    if (alternatePackage == null)
                    {
                        return new UpdateDeprecationError(
                            HttpStatusCode.NotFound,
                            string.Format(Strings.DeprecatePackage_NoAlternatePackage, alternatePackageId, alternatePackageVersion));
                    }
                }
                else
                {
                    alternatePackageRegistration = _packageService.FindPackageRegistrationById(alternatePackageId);
                    if (alternatePackageRegistration == null)
                    {
                        return new UpdateDeprecationError(
                            HttpStatusCode.NotFound,
                            string.Format(Strings.DeprecatePackage_NoAlternatePackageRegistration, alternatePackageId));
                    }
                }
            }

            var packagesToUpdate = new List<Package>();
            foreach (var version in versions)
            {
                var normalizedVersion = NuGetVersionFormatter.Normalize(version);
                var package = packages.SingleOrDefault(v => v.NormalizedVersion == normalizedVersion);
                if (package == null)
                {
                    // This should only happen if someone hacks the form or if a version of the package is deleted while the user is filling out the form.
                    return new UpdateDeprecationError(
                        HttpStatusCode.NotFound,
                        string.Format(Strings.DeprecatePackage_MissingVersion, id));
                }
                else
                {
                    packagesToUpdate.Add(package);
                }
            }

            if (alternatePackageRegistration == registration || packagesToUpdate.Any(p => p == alternatePackage))
            {
                return new UpdateDeprecationError(
                    HttpStatusCode.BadRequest,
                    Strings.DeprecatePackage_AlternateOfSelf);
            }

            await _deprecationService.UpdateDeprecation(
                packagesToUpdate,
                status,
                alternatePackageRegistration,
                alternatePackage,
                customMessage,
                currentUser);

            return null;
        }
    }
}