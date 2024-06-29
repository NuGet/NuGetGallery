// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;

namespace NuGetGallery
{
    public class PackageDeprecationService : IPackageDeprecationService
    {
        private readonly IEntitiesContext _entitiesContext;
        private readonly IPackageUpdateService _packageUpdateService;
        private readonly ITelemetryService _telemetryService;
        private readonly IAuditingService _auditingService;

        public PackageDeprecationService(
           IEntitiesContext entitiesContext,
           IPackageUpdateService packageUpdateService,
           ITelemetryService telemetryService,
           IAuditingService auditingService)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _packageUpdateService = packageUpdateService ?? throw new ArgumentNullException(nameof(packageUpdateService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
        }

        public async Task UpdateDeprecation(
           IReadOnlyList<Package> packages,
           PackageDeprecationStatus status,
           PackageRegistration alternatePackageRegistration,
           Package alternatePackage,
           string customMessage,
           User user,
           ListedVerb listedVerb,
           string auditReason)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (packages == null || !packages.Any())
            {
                throw new ArgumentException(nameof(packages));
            }

            if (auditReason == null)
            {
                throw new ArgumentNullException(nameof(auditReason));
            }

            if (packages.Select(p => p.PackageRegistrationKey).Distinct().Count() > 1)
            {
                throw new ArgumentException("All packages to deprecate must have the same ID.", nameof(packages));
            }

            var shouldDelete = status == PackageDeprecationStatus.NotDeprecated;
            var listed = listedVerb == ListedVerb.Relist;
            var deprecations = new List<PackageDeprecation>();
            var changedPackages = new List<Package>();
            var unchangedPackages = new List<Package>();
            foreach (var package in packages)
            {
                // This change tracking could theoretically be done via the Entity Framework change tracker, but given
                // the low number of properties here, we'll track the changes ourselves. To use the change tracker to
                // check if an individual entity has changed would require non-trivial changes to our DB abstractions.
                var changed = false;
                var deprecation = package.Deprecations.SingleOrDefault();
                if (shouldDelete)
                {
                    if (deprecation != null)
                    {
                        package.Deprecations.Remove(deprecation);
                        deprecations.Add(deprecation);
                        changed = true;
                    }
                }
                else
                {
                    if (deprecation == null)
                    {
                        deprecation = new PackageDeprecation
                        {
                            Package = package
                        };

                        package.Deprecations.Add(deprecation);
                        deprecations.Add(deprecation);
                        changed = true;
                    }

                    if (deprecation.Status != status)
                    {
                        deprecation.Status = status;
                        changed = true;
                    }

                    if (deprecation.AlternatePackageRegistrationKey != alternatePackageRegistration?.Key)
                    {
                        deprecation.AlternatePackageRegistration = alternatePackageRegistration;
                        changed = true;
                    }

                    if (deprecation.AlternatePackageKey != alternatePackage?.Key)
                    {
                        deprecation.AlternatePackage = alternatePackage;
                        changed = true;
                    }

                    if (deprecation.CustomMessage != customMessage)
                    {
                        deprecation.CustomMessage = customMessage;
                        changed = true;
                    }

                    if (changed && deprecation.DeprecatedByUserKey != user?.Key)
                    {
                        deprecation.DeprecatedByUser = user;
                        changed = true;
                    }
                }

                if (listedVerb != ListedVerb.Unchanged && package.Listed != listed)
                {
                    package.Listed = listed;
                    changed = true;
                }

                if (changed)
                {
                    changedPackages.Add(package);
                }
                else
                {
                    unchangedPackages.Add(package);
                }
            }

            if (shouldDelete)
            {
                _entitiesContext.Deprecations.RemoveRange(deprecations);
            }
            else
            {
                _entitiesContext.Deprecations.AddRange(deprecations);
            }

            if (_entitiesContext.HasChanges)
            {
                using (new SuspendDbExecutionStrategy())
                using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
                {
                    await _entitiesContext.SaveChangesAsync();

                    // Ideally, the number of changed packages should be zero if and only if the entity context has
                    // no changes (therefore this line should not be reached). But it is possible that an entity can
                    // be changed for other reasons, such as https://github.com/NuGet/NuGetGallery/issues/9950.
                    // Therefore, allow the transaction to be committed but do not update the LastEdited property on
                    // the package, to avoid unnecessary package edits flowing into V3.
                    if (changedPackages.Count > 0)
                    {
                        await _packageUpdateService.UpdatePackagesAsync(changedPackages);
                    }

                    transaction.Commit();

                    if (changedPackages.Count > 0)
                    {
                        _telemetryService.TrackPackageDeprecate(
                            changedPackages,
                            status,
                            alternatePackageRegistration,
                            alternatePackage,
                            !string.IsNullOrWhiteSpace(customMessage),
                            hasChanges: true);
                    }

                    foreach (var package in changedPackages)
                    {
                        await _auditingService.SaveAuditRecordAsync(
                            new PackageAuditRecord(
                                package,
                                status == PackageDeprecationStatus.NotDeprecated ? AuditedPackageAction.Undeprecate : AuditedPackageAction.Deprecate,
                                auditReason));
                    }
                }
            }

            if (unchangedPackages.Count > 0)
            {
                _telemetryService.TrackPackageDeprecate(
                    unchangedPackages,
                    status,
                    alternatePackageRegistration,
                    alternatePackage,
                    !string.IsNullOrWhiteSpace(customMessage),
                    hasChanges: false);
            }
        }

        public IReadOnlyList<PackageDeprecation> GetDeprecationsById(string id)
        {
            return _entitiesContext.Deprecations
                .Include(d => d.AlternatePackage.PackageRegistration)
                .Include(d => d.AlternatePackageRegistration)
                .Where(d => d.Package.PackageRegistration.Id == id)
                .ToList();
        }
    }
}