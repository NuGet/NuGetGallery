// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGetGallery.Auditing;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class PackageService : CorePackageService, IPackageService
    {
        private readonly IEntityRepository<PackageRegistration> _packageRegistrationRepository;
        private readonly IPackageNamingConflictValidator _packageNamingConflictValidator;
        private readonly IAuditingService _auditingService;
        private readonly ITelemetryService _telemetryService;

        public PackageService(
            IEntityRepository<PackageRegistration> packageRegistrationRepository,
            IEntityRepository<Package> packageRepository,
            IPackageNamingConflictValidator packageNamingConflictValidator,
            IAuditingService auditingService,
            ITelemetryService telemetryService) : base(packageRepository)
        {
            _packageRegistrationRepository = packageRegistrationRepository ?? throw new ArgumentNullException(nameof(packageRegistrationRepository));
            _packageNamingConflictValidator = packageNamingConflictValidator ?? throw new ArgumentNullException(nameof(packageNamingConflictValidator));
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        }

        /// <summary>
        /// When no exceptions thrown, this method ensures the package metadata is valid.
        /// </summary>
        /// <param name="packageArchiveReader">
        /// The <see cref="PackageArchiveReader"/> instance providing the package metadata.
        /// </param>
        /// <exception cref="InvalidPackageException">
        /// This exception will be thrown when a package metadata property violates a data validation constraint.
        /// </exception>
        public void EnsureValid(PackageArchiveReader packageArchiveReader)
        {
            try
            {
                var packageMetadata = PackageMetadata.FromNuspecReader(
                    packageArchiveReader.GetNuspecReader(),
                    strict: true);

                ValidateNuGetPackageMetadata(packageMetadata);

                ValidatePackageTitle(packageMetadata);

                var supportedFrameworks = GetSupportedFrameworks(packageArchiveReader).Select(fn => fn.ToShortNameOrNull()).ToArray();
                if (!supportedFrameworks.AnySafe(sf => sf == null))
                {
                    ValidateSupportedFrameworks(supportedFrameworks);
                }
            }
            catch (Exception exception) when (exception is EntityException || exception is PackagingException)
            {
                // Wrap the exception for consistency of this API.
                throw new InvalidPackageException(exception.Message, exception);
            }
        }

        /// <summary>
        /// Validates and creates a <see cref="Package"/> entity from a NuGet package archive.
        /// </summary>
        /// <param name="nugetPackage">A <see cref="PackageArchiveReader"/> instance from which package metadata can be read.</param>
        /// <param name="packageStreamMetadata">The <see cref="PackageStreamMetadata"/> instance providing metadata about the package stream.</param>
        /// <param name="owner">The <see cref="User"/> creating the package.</param>
        /// <param name="commitChanges"><c>True</c> to commit the changes to the data store and notify the indexing service; otherwise <c>false</c>.</param>
        /// <returns>Returns the created <see cref="Package"/> entity.</returns>
        /// <exception cref="InvalidPackageException">
        /// This exception will be thrown when a package metadata property violates a data validation constraint.
        /// </exception>
        public async Task<Package> CreatePackageAsync(PackageArchiveReader nugetPackage, PackageStreamMetadata packageStreamMetadata, User owner, User currentUser, bool isVerified)
        {
            PackageMetadata packageMetadata;
            PackageRegistration packageRegistration;

            try
            {
                packageMetadata = PackageMetadata.FromNuspecReader(
                    nugetPackage.GetNuspecReader(),
                    strict: true);

                ValidateNuGetPackageMetadata(packageMetadata);

                ValidatePackageTitle(packageMetadata);

                packageRegistration = CreateOrGetPackageRegistration(owner, currentUser, packageMetadata, isVerified);
            }
            catch (Exception exception) when (exception is EntityException || exception is PackagingException)
            {
                // Wrap the exception for consistency of this API.
                throw new InvalidPackageException(exception.Message, exception);
            }

            var package = CreatePackageFromNuGetPackage(packageRegistration, nugetPackage, packageMetadata, packageStreamMetadata, currentUser);
            packageRegistration.Packages.Add(package);
            await UpdateIsLatestAsync(packageRegistration, commitChanges: false);

            return package;
        }

        public virtual PackageRegistration FindPackageRegistrationById(string id)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            return _packageRegistrationRepository.GetAll()
                .Include(pr => pr.Owners)
                .SingleOrDefault(pr => pr.Id == id);
        }

        public virtual Package FindPackageByIdAndVersion(
            string id,
            string version,
            int? semVerLevelKey = null,
            bool allowPrerelease = true)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            Package package = null;
            if (!string.IsNullOrEmpty(version))
            {
                package = FindPackageByIdAndVersionStrict(id, version);
            }

            // Package version not found: fallback to latest version.
            if (package == null)
            {
                // Optimization: Every time we look at a package we almost always want to see
                // all the other packages with the same ID via the PackageRegistration property.
                // This resulted in a gnarly query.
                // Instead, we can always query for all packages with the ID.
                IEnumerable<Package> packagesQuery = GetPackagesByIdQueryable(id);

                if (string.IsNullOrEmpty(version) && !allowPrerelease)
                {
                    // If there's a specific version given, don't bother filtering by prerelease. 
                    // You could be asking for a prerelease package.
                    packagesQuery = packagesQuery.Where(p => !p.IsPrerelease);
                }

                var packageVersions = packagesQuery.ToList();

                // Fallback behavior: collect the latest version.
                // Check SemVer-level and allow-prerelease constraints.
                if (semVerLevelKey == SemVerLevelKey.SemVer2)
                {
                    package = packageVersions.FirstOrDefault(p => p.IsLatestStableSemVer2);

                    if (package == null && allowPrerelease)
                    {
                        package = packageVersions.FirstOrDefault(p => p.IsLatestSemVer2);
                    }
                }

                // Fallback behavior: collect the latest version.
                // If SemVer-level is not defined, 
                // or SemVer-level = 2.0.0 and no package was marked as SemVer2-latest,
                // then check for packages marked as non-SemVer2 latest.
                if (semVerLevelKey == SemVerLevelKey.Unknown
                    || (semVerLevelKey == SemVerLevelKey.SemVer2 && package == null))
                {
                    package = packageVersions.FirstOrDefault(p => p.IsLatestStable);

                    if (package == null && allowPrerelease)
                    {
                        package = packageVersions.FirstOrDefault(p => p.IsLatest);
                    }
                }

                // If we couldn't find a package marked as latest, then
                // return the most recent one (prerelease ones were already filtered out if appropriate...)
                if (package == null)
                {
                    package = packageVersions.OrderByDescending(p => p.Version).FirstOrDefault();
                }
            }

            return package;
        }

        public virtual Package FindAbsoluteLatestPackageById(string id, int? semVerLevelKey)
        {
            var packageVersions = GetPackagesByIdQueryable(id);

            Package package;
            if (semVerLevelKey == SemVerLevelKey.SemVer2)
            {
                package = packageVersions.FirstOrDefault(p => p.IsLatestSemVer2);
            }
            else
            {
                package = packageVersions.FirstOrDefault(p => p.IsLatest);
            }

            // If we couldn't find a package marked as latest, then return the most recent one 
            if (package == null)
            {
                package = packageVersions.OrderByDescending(p => p.Version).FirstOrDefault();
            }

            return package;
        }

        public IEnumerable<Package> FindPackagesByOwner(User user, bool includeUnlisted, bool includeVersions = false)
        {
            return GetPackagesForOwners(new [] { user.Key }, includeUnlisted, includeVersions);
        }

        /// <summary>
        /// Find packages by owner, including organization owners that the user belongs to.
        /// </summary>
        public IEnumerable<Package> FindPackagesByAnyMatchingOwner(
            User user,
            bool includeUnlisted,
            bool includeVersions = false)
        {
            var ownerKeys = user.Organizations.Select(org => org.OrganizationKey).ToList();
            ownerKeys.Insert(0, user.Key);

            return GetPackagesForOwners(ownerKeys, includeUnlisted, includeVersions);
        }

        private IEnumerable<Package> GetPackagesForOwners(IEnumerable<int> ownerKeys, bool includeUnlisted, bool includeVersions)
        {
            IQueryable<Package> packages = _packageRepository.GetAll()
                .Where(p => p.PackageRegistration.Owners.Any(o => ownerKeys.Contains(o.Key)));

            if (!includeUnlisted)
            {
                packages = packages.Where(p => p.Listed);
            }

            if (includeVersions)
            {
                return packages
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners)
                .ToList();
            }

            // Do a best effort of retrieving the latest version. Note that UpdateIsLatest has had concurrency issues
            // where sometimes packages no rows with IsLatest set. In this case, we'll just select the last inserted
            // row (descending [Key]) as opposed to reading all rows into memory and sorting on NuGetVersion.
            return packages
                .GroupBy(p => p.PackageRegistrationKey)
                .Select(g => g
                    // order booleans desc so that true (1) comes first
                    .OrderByDescending(p => p.IsLatestStableSemVer2)
                    .ThenByDescending(p => p.IsLatestStable)
                    .ThenByDescending(p => p.IsLatestSemVer2)
                    .ThenByDescending(p => p.IsLatest)
                    .ThenByDescending(p => p.Listed)
                    .ThenByDescending(p => p.Key)
                    .FirstOrDefault())
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners)
                .ToList();
        }

        /// <summary>
        /// For a package get the list of owners that are not organizations.
        /// All the resulted user accounts will be distinct.
        /// </summary>
        /// <param name="package">The package.</param>
        /// <returns>The list of package owners.</returns>
        public IEnumerable<User> GetPackageUserAccountOwners(Package package)
        {
            return package.PackageRegistration.Owners
                .SelectMany((owner) =>
                {
                    if (owner is Organization)
                    {
                        return OrganizationExtensions.GetUserAccountMembers((Organization)owner);
                    }
                    return new List<User> { owner };
                })
                .Distinct();
        }

        public IQueryable<PackageRegistration> FindPackageRegistrationsByOwner(User user)
        {
            return _packageRegistrationRepository.GetAll().Where(p => p.Owners.Any(o => o.Key == user.Key));
        }

        public IEnumerable<Package> FindDependentPackages(Package package)
        {
            // Grab all candidates
            var candidateDependents = (from p in _packageRepository.GetAll()
                                       from d in p.Dependencies
                                       where d.Id == package.PackageRegistration.Id
                                       select d).Include(pk => pk.Package.PackageRegistration).ToList();
            // Now filter by version range.
            var packageVersion = new NuGetVersion(package.Version);
            var dependents = from d in candidateDependents
                             where VersionRange.Parse(d.VersionSpec).Satisfies(packageVersion)
                             select d;

            return dependents.Select(d => d.Package);
        }

        public async Task PublishPackageAsync(string id, string version, bool commitChanges = true)
        {
            var package = FindPackageByIdAndVersionStrict(id, version);

            if (package == null)
            {
                throw new EntityException(Strings.PackageWithIdAndVersionNotFound, id, version);
            }

            await PublishPackageAsync(package, commitChanges);
        }

        public async Task PublishPackageAsync(Package package, bool commitChanges = true)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            package.Published = DateTime.UtcNow;
            package.Listed = true;

            await UpdateIsLatestAsync(package.PackageRegistration, commitChanges: false);

            if (commitChanges)
            {
                await _packageRepository.CommitChangesAsync();
            }
        }

        public async Task AddPackageOwnerAsync(PackageRegistration package, User newOwner)
        {
            package.Owners.Add(newOwner);
            await _packageRepository.CommitChangesAsync();
        }

        public async Task RemovePackageOwnerAsync(PackageRegistration package, User user)
        {
            // To support the delete account scenario, the admin can delete the last owner of a package.
            package.Owners.Remove(user);
            await _packageRepository.CommitChangesAsync();
        }

        public async Task MarkPackageListedAsync(Package package, bool commitChanges = true)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (package.Listed)
            {
                return;
            }

            if (package.PackageStatusKey == PackageStatus.Deleted)
            {
                throw new InvalidOperationException("A deleted package should never be listed!");
            }
            if (!package.Listed && (package.IsLatestStable || package.IsLatest))
            {
                throw new InvalidOperationException("An unlisted package should never be latest or latest stable!");
            }

            package.Listed = true;
            package.LastUpdated = DateTime.UtcNow;
            // NOTE: LastEdited will be overwritten by a trigger defined in the migration named "AddTriggerForPackagesLastEdited".
            package.LastEdited = DateTime.UtcNow;

            await UpdateIsLatestAsync(package.PackageRegistration, commitChanges: false);

            await _auditingService.SaveAuditRecordAsync(new PackageAuditRecord(package, AuditedPackageAction.List));

            _telemetryService.TrackPackageListed(package);

            if (commitChanges)
            {
                await _packageRepository.CommitChangesAsync();
            }
        }

        public async Task MarkPackageUnlistedAsync(Package package, bool commitChanges = true)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }
            if (!package.Listed)
            {
                return;
            }

            package.Listed = false;
            package.LastUpdated = DateTime.UtcNow;
            // NOTE: LastEdited will be overwritten by a trigger defined in the migration named "AddTriggerForPackagesLastEdited".
            package.LastEdited = DateTime.UtcNow;

            if (package.IsLatest || package.IsLatestStable)
            {
                await UpdateIsLatestAsync(package.PackageRegistration, commitChanges: false);
            }

            await _auditingService.SaveAuditRecordAsync(new PackageAuditRecord(package, AuditedPackageAction.Unlist));

            _telemetryService.TrackPackageUnlisted(package);

            if (commitChanges)
            {
                await _packageRepository.CommitChangesAsync();
            }
        }

        private PackageRegistration CreateOrGetPackageRegistration(User owner, User currentUser, PackageMetadata packageMetadata, bool isVerified)
        {
            var packageRegistration = FindPackageRegistrationById(packageMetadata.Id);
            
            if (packageRegistration == null)
            {
                if (_packageNamingConflictValidator.IdConflictsWithExistingPackageTitle(packageMetadata.Id))
                {
                    throw new EntityException(Strings.NewRegistrationIdMatchesExistingPackageTitle, packageMetadata.Id);
                }

                packageRegistration = new PackageRegistration
                {
                    Id = packageMetadata.Id,
                    IsVerified = isVerified
                };

                packageRegistration.Owners.Add(owner);

                _packageRegistrationRepository.InsertOnCommit(packageRegistration);
            }

            return packageRegistration;
        }

        private Package CreatePackageFromNuGetPackage(PackageRegistration packageRegistration, PackageArchiveReader nugetPackage, PackageMetadata packageMetadata, PackageStreamMetadata packageStreamMetadata, User user)
        {
            var package = packageRegistration.Packages.SingleOrDefault(pv => pv.Version == packageMetadata.Version.OriginalVersion);

            if (package != null)
            {
                throw new EntityException(
                    "A package with identifier '{0}' and version '{1}' already exists.", packageRegistration.Id, package.Version);
            }

            package = new Package();
            package.PackageRegistration = packageRegistration;

            package = EnrichPackageFromNuGetPackage(package, nugetPackage, packageMetadata, packageStreamMetadata, user);

            return package;
        }

        public virtual Package EnrichPackageFromNuGetPackage(
            Package package,
            PackageArchiveReader packageArchive,
            PackageMetadata packageMetadata,
            PackageStreamMetadata packageStreamMetadata,
            User user)
        {
            // Version must always be the exact string from the nuspec, which OriginalVersion will return to us.
            // However, we do also store a normalized copy for looking up later.
            package.Version = packageMetadata.Version.OriginalVersion;
            package.NormalizedVersion = packageMetadata.Version.ToNormalizedString();

            package.Description = packageMetadata.Description;
            package.ReleaseNotes = packageMetadata.ReleaseNotes;
            package.HashAlgorithm = packageStreamMetadata.HashAlgorithm;
            package.Hash = packageStreamMetadata.Hash;
            package.PackageFileSize = packageStreamMetadata.Size;
            package.Language = packageMetadata.Language;
            package.Copyright = packageMetadata.Copyright;
            package.FlattenedAuthors = packageMetadata.Authors.Flatten();
            package.IsPrerelease = packageMetadata.Version.IsPrerelease;
            package.Listed = true;
            package.RequiresLicenseAcceptance = packageMetadata.RequireLicenseAcceptance;
            package.Summary = packageMetadata.Summary;
            package.Tags = PackageHelper.ParseTags(packageMetadata.Tags);
            package.Title = packageMetadata.Title;
            package.User = user;

            package.IconUrl = packageMetadata.IconUrl.ToEncodedUrlStringOrNull();
            package.LicenseUrl = packageMetadata.LicenseUrl.ToEncodedUrlStringOrNull();
            package.ProjectUrl = packageMetadata.ProjectUrl.ToEncodedUrlStringOrNull();
            package.MinClientVersion = packageMetadata.MinClientVersion.ToStringOrNull();

#pragma warning disable 618 // TODO: remove Package.Authors completely once production services definitely no longer need it
            foreach (var author in packageMetadata.Authors)
            {
                package.Authors.Add(new PackageAuthor { Name = author });
            }
#pragma warning restore 618

            var supportedFrameworks = GetSupportedFrameworks(packageArchive)
                .ToArray();

            if (!supportedFrameworks.Any(fx => fx != null && fx.IsAny))
            {
                var supportedFrameworkNames = supportedFrameworks
                                .Select(fn => fn.ToShortNameOrNull())
                                .Where(fn => fn != null)
                                .ToArray();

                ValidateSupportedFrameworks(supportedFrameworkNames);

                foreach (var supportedFramework in supportedFrameworkNames)
                {
                    package.SupportedFrameworks.Add(new PackageFramework { TargetFramework = supportedFramework });
                }
            }

            package.Dependencies = packageMetadata
                .GetDependencyGroups()
                .AsPackageDependencyEnumerable()
                .ToList();

            package.PackageTypes = packageMetadata
                .GetPackageTypes()
                .AsPackageTypeEnumerable()
                .ToList();

            package.FlattenedDependencies = package.Dependencies.Flatten();

            package.FlattenedPackageTypes = package.PackageTypes.Flatten();

            // Identify the SemVerLevelKey using the original package version string and package dependencies
            package.SemVerLevelKey = SemVerLevelKey.ForPackage(packageMetadata.Version, package.Dependencies);

            return package;
        }

        public virtual IEnumerable<NuGetFramework> GetSupportedFrameworks(PackageArchiveReader package)
        {
            return package.GetSupportedFrameworks();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private static void ValidateNuGetPackageMetadata(PackageMetadata packageMetadata)
        {
            // TODO: Change this to use DataAnnotations
            if (packageMetadata.Id.Length > CoreConstants.MaxPackageIdLength)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Id", CoreConstants.MaxPackageIdLength);
            }
            if (packageMetadata.Authors != null && packageMetadata.Authors.Flatten().Length > 4000)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Authors", "4000");
            }
            if (packageMetadata.Copyright != null && packageMetadata.Copyright.Length > 4000)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Copyright", "4000");
            }
            if (packageMetadata.Description == null)
            {
                throw new EntityException(Strings.NuGetPackagePropertyMissing, "Description");
            }
            else if (packageMetadata.Description != null && packageMetadata.Description.Length > 4000)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Description", "4000");
            }
            if (packageMetadata.IconUrl != null && packageMetadata.IconUrl.AbsoluteUri.Length > 4000)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "IconUrl", "4000");
            }
            if (packageMetadata.LicenseUrl != null && packageMetadata.LicenseUrl.AbsoluteUri.Length > 4000)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "LicenseUrl", "4000");
            }
            if (packageMetadata.ProjectUrl != null && packageMetadata.ProjectUrl.AbsoluteUri.Length > 4000)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "ProjectUrl", "4000");
            }
            if (packageMetadata.Summary != null && packageMetadata.Summary.Length > 4000)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Summary", "4000");
            }
            if (packageMetadata.Tags != null && packageMetadata.Tags.Length > 4000)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Tags", "4000");
            }
            if (packageMetadata.Title != null && packageMetadata.Title.Length > 256)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Title", "256");
            }

            if (packageMetadata.Version != null && packageMetadata.Version.ToFullString().Length > 64)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Version", "64");
            }

            if (packageMetadata.Language != null && packageMetadata.Language.Length > 20)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Language", "20");
            }

            // Validate dependencies
            if (packageMetadata.GetDependencyGroups() != null)
            {
                var packageDependencies = packageMetadata.GetDependencyGroups().ToList();

                foreach (var dependency in packageDependencies.SelectMany(s => s.Packages))
                {
                    // NuGet.Core compatibility - dependency package id can not be > 128 characters
                    if (dependency.Id != null && dependency.Id.Length > CoreConstants.MaxPackageIdLength)
                    {
                        throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Dependency.Id", CoreConstants.MaxPackageIdLength);
                    }

                    // NuGet.Core compatibility - dependency versionspec can not be > 256 characters
                    if (dependency.VersionRange != null && dependency.VersionRange.ToString().Length > 256)
                    {
                        throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Dependency.VersionSpec", "256");
                    }
                }

                // NuGet.Core compatibility - flattened dependencies should be < Int16.MaxValue
                if (packageDependencies.Flatten().Length > Int16.MaxValue)
                {
                    throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Dependencies", Int16.MaxValue);
                }
            }
        }

        private static void ValidateSupportedFrameworks(string[] supportedFrameworks)
        {
            // Frameworks within the portable profile are not allowed to have profiles themselves.
            // Ensure portable framework does not contain more than one hyphen.
            var invalidPortableFramework = supportedFrameworks.FirstOrDefault(fx =>
                !string.IsNullOrEmpty(fx)
                && fx.StartsWith("portable-", StringComparison.OrdinalIgnoreCase)
                && fx.Split('-').Length > 2);

            if (invalidPortableFramework != null)
            {
                throw new EntityException(
                    Strings.InvalidPortableFramework, invalidPortableFramework);
            }
        }

        private void ValidatePackageTitle(PackageMetadata packageMetadata)
        {
            if (_packageNamingConflictValidator.TitleConflictsWithExistingRegistrationId(packageMetadata.Id, packageMetadata.Title))
            {
                throw new EntityException(Strings.TitleMatchesExistingRegistration, packageMetadata.Title);
            }
        }

        public async Task SetLicenseReportVisibilityAsync(Package package, bool visible, bool commitChanges = true)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }
            package.HideLicenseReport = !visible;
            if (commitChanges)
            {
                await _packageRepository.CommitChangesAsync();
            }
        }

        public async Task IncrementDownloadCountAsync(string id, string version, bool commitChanges = true)
        {
            var package = FindPackageByIdAndVersionStrict(id, version);
            if (package != null)
            {
                package.DownloadCount++;
                package.PackageRegistration.DownloadCount++;
                if (commitChanges)
                {
                    await _packageRepository.CommitChangesAsync();
                }
            }
        }

        public virtual async Task UpdatePackageVerifiedStatusAsync(IReadOnlyCollection<PackageRegistration> packageRegistrationList, bool isVerified)
        {
            var packageRegistrationIdSet = new HashSet<string>(packageRegistrationList.Select(prl => prl.Id));
            var allPackageRegistrations = _packageRegistrationRepository.GetAll();
            var packageRegistrationsToUpdate = allPackageRegistrations
                .Where(pr => packageRegistrationIdSet.Contains(pr.Id))
                .ToList();

            if (packageRegistrationsToUpdate.Count > 0)
            {
                packageRegistrationsToUpdate
                    .ForEach(pru => pru.IsVerified = isVerified);

                await _packageRegistrationRepository.CommitChangesAsync();
            }
        }
    }
}