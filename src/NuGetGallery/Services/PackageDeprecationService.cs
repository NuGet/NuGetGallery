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
           User user)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (packages == null || !packages.Any())
            {
                throw new ArgumentException(nameof(packages));
            }

            var registration = packages.First().PackageRegistration;
            if (packages.Select(p => p.PackageRegistrationKey).Distinct().Count() > 1)
            {
                throw new ArgumentException("All packages to deprecate must have the same ID.", nameof(packages));
            }

            using (var strategy = new SuspendDbExecutionStrategy())
            using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
            {
                var shouldDelete = status == PackageDeprecationStatus.NotDeprecated;
                var deprecations = new List<PackageDeprecation>();
                foreach (var package in packages)
                {
                    var deprecation = package.Deprecations.SingleOrDefault();
                    if (shouldDelete)
                    {
                        if (deprecation != null)
                        {
                            package.Deprecations.Remove(deprecation);
                            deprecations.Add(deprecation);
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
                        }

                        deprecation.Status = status;
                        deprecation.DeprecatedByUser = user;

                        deprecation.AlternatePackageRegistration = alternatePackageRegistration;
                        deprecation.AlternatePackage = alternatePackage;

                        deprecation.CustomMessage = customMessage;
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

                await _entitiesContext.SaveChangesAsync();

                await _packageUpdateService.UpdatePackagesAsync(packages);

                transaction.Commit();

                _telemetryService.TrackPackageDeprecate(
                    packages,
                    status,
                    alternatePackageRegistration,
                    alternatePackage,
                    !string.IsNullOrWhiteSpace(customMessage));

                foreach (var package in packages)
                {
                    await _auditingService.SaveAuditRecordAsync(
                        new PackageAuditRecord(
                            package,
                            status == PackageDeprecationStatus.NotDeprecated ? AuditedPackageAction.Undeprecate : AuditedPackageAction.Deprecate,
                            status == PackageDeprecationStatus.NotDeprecated ? PackageUndeprecatedVia.Web : PackageDeprecatedVia.Web));
                }
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