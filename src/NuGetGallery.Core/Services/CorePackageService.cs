// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class CorePackageService : ICorePackageService
    {
        protected readonly IEntityRepository<Certificate> _certificateRepository;
        protected readonly IEntityRepository<Package> _packageRepository;
        protected readonly IEntityRepository<PackageRegistration> _packageRegistrationRepository;

        public CorePackageService(
            IEntityRepository<Package> packageRepository,
            IEntityRepository<PackageRegistration> packageRegistrationRepository,
            IEntityRepository<Certificate> certificateRepository)
        {
            _packageRepository = packageRepository ?? throw new ArgumentNullException(nameof(packageRepository));
            _packageRegistrationRepository = packageRegistrationRepository ?? throw new ArgumentNullException(nameof(packageRegistrationRepository));
            _certificateRepository = certificateRepository ?? throw new ArgumentNullException(nameof(certificateRepository));
        }

        public virtual async Task UpdatePackageStreamMetadataAsync(
            Package package,
            PackageStreamMetadata metadata,
            bool commitChanges = true)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            package.Hash = metadata.Hash;
            package.HashAlgorithm = metadata.HashAlgorithm;
            package.PackageFileSize = metadata.Size;

            var now = DateTime.UtcNow;
            package.LastUpdated = now;

            /// If the package is available, consider this change as an "edit" so that the package appears for cursors
            /// on the <see cref="Package.LastEdited"/> field.
            if (package.PackageStatusKey == PackageStatus.Available)
            {
                package.LastEdited = now;
            }

            if (commitChanges)
            {
                await _packageRepository.CommitChangesAsync();
            }
        }

        public virtual async Task UpdatePackageStatusAsync(
            Package package,
            PackageStatus newPackageStatus,
            bool commitChanges = true)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (package.PackageRegistration == null)
            {
                throw new ArgumentException($"The {nameof(Package.PackageRegistration)} property must populated on the provided package.", nameof(package));
            }

            // Avoid all of this work if the package status is not changing.
            if (package.PackageStatusKey != newPackageStatus)
            {
#pragma warning disable CS0612 // Type or member is obsolete
                package.Deleted = newPackageStatus == PackageStatus.Deleted;
#pragma warning restore CS0612 // Type or member is obsolete
                package.PackageStatusKey = newPackageStatus;
                package.LastUpdated = DateTime.UtcNow;

                /// If the package is being made available, consider this change as an "edit" so that the package
                /// appears for cursors on the <see cref="Package.LastEdited"/> field.
                if (newPackageStatus == PackageStatus.Available)
                {
                    package.LastEdited = DateTime.UtcNow;
                }

                // If the package is just now becoming available or if it was previously a latest package, then
                // re-evaluate all of the latest bits.
                if (newPackageStatus == PackageStatus.Available ||
                    package.IsLatest ||
                    package.IsLatestStable ||
                    package.IsLatestSemVer2 ||
                    package.IsLatestStableSemVer2)
                {
                    await UpdateIsLatestAsync(package.PackageRegistration, commitChanges: false);
                }
            }

            if (commitChanges)
            {
                // Even if the package status did not change, commit the changes in the entity context to pick up any
                // other entity changes.
                await _packageRepository.CommitChangesAsync();
            }
        }

        public virtual async Task UpdateIsLatestAsync(PackageRegistration packageRegistration, bool commitChanges = true)
        {
            if (!packageRegistration.Packages.Any())
            {
                return;
            }

            // TODO: improve setting the latest bit; this is horrible. Trigger maybe?
            var currentUtcTime = DateTime.UtcNow;
            foreach (var pv in packageRegistration.Packages.Where(p => p.IsLatest || p.IsLatestStable || p.IsLatestSemVer2 || p.IsLatestStableSemVer2))
            {
                pv.IsLatest = false;
                pv.IsLatestStable = false;
                pv.IsLatestSemVer2 = false;
                pv.IsLatestStableSemVer2 = false;
                pv.LastUpdated = currentUtcTime;
            }

            // If the last listed package was just unlisted, then we won't find another one
            var latestPackage = FindPackage(
                packageRegistration.Packages.AsQueryable(),
                ps => ps
                    .Where(SemVerLevelKey.IsUnknownPredicate())
                    .Where(p => p.PackageStatusKey == PackageStatus.Available && p.Listed));

            var latestSemVer2Package = FindPackage(
                packageRegistration.Packages.AsQueryable(),
                ps => ps
                    .Where(SemVerLevelKey.IsSemVer2Predicate())
                    .Where(p => p.PackageStatusKey == PackageStatus.Available && p.Listed));

            if (latestPackage != null)
            {
                latestPackage.IsLatest = true;
                latestPackage.LastUpdated = currentUtcTime;

                if (latestPackage.IsPrerelease)
                {
                    // If the newest uploaded package is a prerelease package, we need to find an older package that is
                    // a release version and set it to IsLatest.
                    var latestReleasePackage = FindPackage(packageRegistration
                        .Packages
                        .AsQueryable()
                        .Where(SemVerLevelKey.IsUnknownPredicate())
                        .Where(p => !p.IsPrerelease && p.PackageStatusKey == PackageStatus.Available && p.Listed));

                    if (latestReleasePackage != null)
                    {
                        // We could have no release packages
                        latestReleasePackage.IsLatestStable = true;
                        latestReleasePackage.LastUpdated = currentUtcTime;
                    }
                }
                else
                {
                    // Only release versions are marked as IsLatestStable.
                    latestPackage.IsLatestStable = true;
                }
            }

            if (latestSemVer2Package != null)
            {
                latestSemVer2Package.IsLatestSemVer2 = true;
                latestSemVer2Package.LastUpdated = currentUtcTime;

                if (latestSemVer2Package.IsPrerelease)
                {
                    // If the newest uploaded package is a prerelease package, we need to find an older package that is
                    // a release version and set it to IsLatest.
                    var latestSemVer2ReleasePackage = FindPackage(packageRegistration
                        .Packages
                        .AsQueryable()
                        .Where(SemVerLevelKey.IsSemVer2Predicate())
                        .Where(p => !p.IsPrerelease && p.PackageStatusKey == PackageStatus.Available && p.Listed));

                    if (latestSemVer2ReleasePackage != null)
                    {
                        // We could have no release packages
                        latestSemVer2ReleasePackage.IsLatestStableSemVer2 = true;
                        latestSemVer2ReleasePackage.LastUpdated = currentUtcTime;
                    }
                }
                else
                {
                    // Only release versions are marked as IsLatestStable.
                    latestSemVer2Package.IsLatestStableSemVer2 = true;
                }
            }

            if (commitChanges)
            {
                await _packageRepository.CommitChangesAsync();
            }
        }

        public virtual Package FindPackageByIdAndVersionStrict(string id, string version)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrEmpty(version))
            {
                throw new ArgumentException(nameof(version));
            }

            var normalizedVersion = NuGetVersionFormatter.Normalize(version);

            // These string comparisons are case-(in)sensitive depending on SQLServer collation.
            // Case-insensitive collation is recommended, e.g. SQL_Latin1_General_CP1_CI_AS.
            var package = GetPackagesByIdQueryable(id)
                .SingleOrDefault(p => p.NormalizedVersion == normalizedVersion);

            return package;
        }

        public virtual PackageRegistration FindPackageRegistrationById(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentException(CoreStrings.ArgumentCannotBeNullOrEmpty, nameof(packageId));
            }

            return _packageRegistrationRepository.GetAll()
                .Include(pr => pr.Owners.Select(o => o.UserCertificates))
                .Include(pr => pr.RequiredSigners.Select(rs => rs.UserCertificates))
                .Where(registration => registration.Id == packageId)
                .SingleOrDefault();
        }

        public virtual async Task UpdatePackageSigningCertificateAsync(string packageId, string packageVersion, string thumbprint)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException(CoreStrings.ArgumentCannotBeNullOrEmpty, nameof(packageId));
            }

            if (string.IsNullOrEmpty(packageVersion))
            {
                throw new ArgumentException(CoreStrings.ArgumentCannotBeNullOrEmpty, nameof(packageVersion));
            }

            if (string.IsNullOrEmpty(thumbprint))
            {
                throw new ArgumentException(CoreStrings.ArgumentCannotBeNullOrEmpty, nameof(thumbprint));
            }

            var package = FindPackageByIdAndVersionStrict(packageId, packageVersion);

            if (package == null)
            {
                throw new ArgumentException(CoreStrings.PackageNotFound);
            }

            var certificate = _certificateRepository.GetAll()
                .Where(c => c.Thumbprint == thumbprint)
                .SingleOrDefault();

            if (certificate == null)
            {
                throw new ArgumentException(CoreStrings.CertificateNotFound);
            }

            if (package.CertificateKey != certificate.Key)
            {
                package.Certificate = certificate;

                await _packageRepository.CommitChangesAsync();
            }
        }

        protected IQueryable<Package> GetPackagesByIdQueryable(
            string id, 
            PackageDeprecationFieldsToInclude deprecationFields = PackageDeprecationFieldsToInclude.None)
        {
            bool includeDeprecation;
            bool includeDeprecationRelationships;

            switch (deprecationFields)
            {
                case PackageDeprecationFieldsToInclude.None:
                    includeDeprecation = false;
                    includeDeprecationRelationships = false;
                    break;

                case PackageDeprecationFieldsToInclude.Deprecation:
                    includeDeprecation = true;
                    includeDeprecationRelationships = false;
                    break;

                case PackageDeprecationFieldsToInclude.DeprecationAndRelationships:
                    includeDeprecation = true;
                    includeDeprecationRelationships = true;
                    break;

                default:
                    throw new NotSupportedException($"Unknown deprecation fields '{deprecationFields}'");
            }

            return GetPackagesByIdQueryable(
                id,
                includeLicenseReports: true,
                includePackageRegistration: true,
                includeUser: true,
                includeSymbolPackages: true,
                includeDeprecation: includeDeprecation,
                includeDeprecationRelationships: includeDeprecationRelationships);
        }

        protected IQueryable<Package> GetPackagesByIdQueryable(
            string id,
            bool includeLicenseReports,
            bool includePackageRegistration,
            bool includeUser,
            bool includeSymbolPackages,
            bool includeDeprecation,
            bool includeDeprecationRelationships)
        {
            var packages = _packageRepository
                .GetAll()
                .Where(p => p.PackageRegistration.Id == id);

            if (includeLicenseReports)
            {
                packages = packages.Include(p => p.LicenseReports);
            }

            if (includePackageRegistration)
            {
                packages = packages.Include(p => p.PackageRegistration);
            }

            if (includeUser)
            {
                packages = packages.Include(p => p.User);
            }

            if (includeSymbolPackages)
            {
                packages = packages.Include(p => p.SymbolPackages);
            }

            if (includeDeprecationRelationships)
            {
                packages = packages
                    .Include(p => p.Deprecations.Select(d => d.AlternatePackage.PackageRegistration))
                    .Include(p => p.Deprecations.Select(d => d.AlternatePackageRegistration));
            }
            else if (includeDeprecation)
            {
                packages = packages.Include(p => p.Deprecations);
            }

            return packages;
        }

        private static Package FindPackage(IQueryable<Package> packages, Func<IQueryable<Package>, IQueryable<Package>> predicate = null)
        {
            if (predicate != null)
            {
                packages = predicate(packages);
            }

            NuGetVersion version = packages.Max(p => new NuGetVersion(p.Version));
            if (version == null)
            {
                return null;
            }

            return packages.First(pv => pv.Version.Equals(version.OriginalVersion, StringComparison.OrdinalIgnoreCase));
        }
    }
}