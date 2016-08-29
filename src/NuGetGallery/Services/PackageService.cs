// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Versioning;
using NuGetGallery.Auditing;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class PackageService : IPackageService
    {
        private readonly IIndexingService _indexingService;
        private readonly IEntityRepository<PackageOwnerRequest> _packageOwnerRequestRepository;
        private readonly IEntityRepository<PackageRegistration> _packageRegistrationRepository;
        private readonly IEntityRepository<Package> _packageRepository;
        private readonly IPackageNamingConflictValidator _packageNamingConflictValidator;
        private readonly AuditingService _auditingService;

        public PackageService(
            IEntityRepository<PackageRegistration> packageRegistrationRepository,
            IEntityRepository<Package> packageRepository,
            IEntityRepository<PackageOwnerRequest> packageOwnerRequestRepository,
            IIndexingService indexingService,
            IPackageNamingConflictValidator packageNamingConflictValidator,
            AuditingService auditingService)
        {
            if (packageRegistrationRepository == null)
            {
                throw new ArgumentNullException(nameof(packageRegistrationRepository));
            }

            if (packageRepository == null)
            {
                throw new ArgumentNullException(nameof(packageRepository));
            }

            if (packageOwnerRequestRepository == null)
            {
                throw new ArgumentNullException(nameof(packageOwnerRequestRepository));
            }

            if (indexingService == null)
            {
                throw new ArgumentNullException(nameof(indexingService));
            }

            if (packageNamingConflictValidator == null)
            {
                throw new ArgumentNullException(nameof(packageNamingConflictValidator));
            }

            if (auditingService == null)
            {
                throw new ArgumentNullException(nameof(auditingService));
            }

            _packageRegistrationRepository = packageRegistrationRepository;
            _packageRepository = packageRepository;
            _packageOwnerRequestRepository = packageOwnerRequestRepository;
            _indexingService = indexingService;
            _packageNamingConflictValidator = packageNamingConflictValidator;
            _auditingService = auditingService;
        }

        public void EnsureValid(PackageArchiveReader packageArchiveReader)
        {
            var packageMetadata = PackageMetadata.FromNuspecReader(packageArchiveReader.GetNuspecReader());

            ValidateNuGetPackageMetadata(packageMetadata);

            ValidatePackageTitle(packageMetadata);

            var supportedFrameworks = GetSupportedFrameworks(packageArchiveReader).Select(fn => fn.ToShortNameOrNull()).ToArray();
            if (!supportedFrameworks.AnySafe(sf => sf == null))
            {
                ValidateSupportedFrameworks(supportedFrameworks);
            }
        }

        public async Task<Package> CreatePackageAsync(PackageArchiveReader nugetPackage, PackageStreamMetadata packageStreamMetadata, User user, bool commitChanges = true)
        {
            var packageMetadata = PackageMetadata.FromNuspecReader(nugetPackage.GetNuspecReader());

            ValidateNuGetPackageMetadata(packageMetadata);

            ValidatePackageTitle(packageMetadata);

            var packageRegistration = CreateOrGetPackageRegistration(user, packageMetadata);

            var package = CreatePackageFromNuGetPackage(packageRegistration, nugetPackage, packageMetadata, packageStreamMetadata, user);
            packageRegistration.Packages.Add(package);
            await UpdateIsLatestAsync(packageRegistration, false);

            if (commitChanges)
            {
                await _packageRegistrationRepository.CommitChangesAsync();
                NotifyIndexingService();
            }

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

        public virtual Package FindPackageByIdAndVersion(string id, string version, bool allowPrerelease = true)
        {
            if (String.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            // Optimization: Everytime we look at a package we almost always want to see
            // all the other packages with the same ID via the PackageRegistration property.
            // This resulted in a gnarly query.
            // Instead, we can always query for all packages with the ID.
            IEnumerable<Package> packagesQuery = _packageRepository.GetAll()
                .Include(p => p.LicenseReports)
                .Include(p => p.PackageRegistration)
                .Where(p => (p.PackageRegistration.Id == id));

            if (String.IsNullOrEmpty(version) && !allowPrerelease)
            {
                // If there's a specific version given, don't bother filtering by prerelease. You could be asking for a prerelease package.
                packagesQuery = packagesQuery.Where(p => !p.IsPrerelease);
            }

            var packageVersions = packagesQuery.ToList();

            Package package;
            if (String.IsNullOrEmpty(version))
            {
                package = packageVersions.FirstOrDefault(p => p.IsLatestStable);

                if (package == null && allowPrerelease)
                {
                    package = packageVersions.FirstOrDefault(p => p.IsLatest);
                }

                // If we couldn't find a package marked as latest, then
                // return the most recent one (prerelease ones were already filtered out if appropriate...)
                if (package == null)
                {
                    package = packageVersions.OrderByDescending(p => p.Version).FirstOrDefault();
                }
            }
            else
            {
                package = packageVersions.SingleOrDefault(
                    p => p.PackageRegistration.Id.Equals(id, StringComparison.OrdinalIgnoreCase) &&
                         (
                            String.Equals(p.NormalizedVersion, NuGetVersionNormalizer.Normalize(version), StringComparison.OrdinalIgnoreCase)
                         ));
            }
            return package;
        }

        public IEnumerable<Package> FindPackagesByOwner(User user, bool includeUnlisted)
        {
            // Like DisplayPackage we should prefer to show you information from the latest stable version,
            // but show you the latest version (potentially latest UNLISTED version) otherwise.

            IQueryable<Package> latestStablePackageVersions = _packageRepository.GetAll()
                .Where(p =>
                    p.PackageRegistration.Owners.Any(owner => owner.Key == user.Key)
                    && p.IsLatestStable)
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners);

            var latestPackageVersions = _packageRepository.GetAll()
                .Where(p =>
                    p.PackageRegistration.Owners.Any(owner => owner.Key == user.Key)
                    && p.IsLatest)
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners);

            if (includeUnlisted)
            {
                latestPackageVersions = _packageRegistrationRepository.GetAll()
                .Where(pr => pr.Owners.Where(owner => owner.Username == user.Username).Any())
                .Select(pr => pr.Packages.OrderByDescending(p => p.Version).FirstOrDefault())
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners);
            }

            var mergedResults = new Dictionary<string, Package>(StringComparer.OrdinalIgnoreCase);
            foreach (var package in latestPackageVersions.Where(p => p != null))
            {
                if (mergedResults.ContainsKey(package.PackageRegistration.Id)
                    && mergedResults[package.PackageRegistration.Id].Created < package.Created)
                {
                    mergedResults[package.PackageRegistration.Id] = package;
                }
                else
                {
                    mergedResults.Add(package.PackageRegistration.Id, package);
                }
            }

            foreach (var package in latestStablePackageVersions.Where(p => p != null))
            {
                mergedResults[package.PackageRegistration.Id] = package;
            }

            return mergedResults.Values;
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
            var package = FindPackageByIdAndVersion(id, version);

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

            await UpdateIsLatestAsync(package.PackageRegistration, false);

            if (commitChanges)
            {
                await _packageRepository.CommitChangesAsync();
            }
        }

        public async Task AddPackageOwnerAsync(PackageRegistration package, User user)
        {
            package.Owners.Add(user);
            await _packageRepository.CommitChangesAsync();

            var request = FindExistingPackageOwnerRequest(package, user);
            if (request != null)
            {
                _packageOwnerRequestRepository.DeleteOnCommit(request);
                await _packageOwnerRequestRepository.CommitChangesAsync();
            }
            
            await _auditingService.SaveAuditRecord(
                new PackageRegistrationAuditRecord(package, AuditedPackageRegistrationAction.AddOwner, user.Username));
        }

        public async Task RemovePackageOwnerAsync(PackageRegistration package, User user)
        {
            if (package.Owners.Count == 1 && user == package.Owners.Single())
            {
                throw new InvalidOperationException("You can't remove the only owner from a package.");
            }

            var pendingOwner = FindExistingPackageOwnerRequest(package, user);
            if (pendingOwner != null)
            {
                _packageOwnerRequestRepository.DeleteOnCommit(pendingOwner);
                await _packageOwnerRequestRepository.CommitChangesAsync();
                return;
            }

            package.Owners.Remove(user);
            await _packageRepository.CommitChangesAsync();

            await _auditingService.SaveAuditRecord(
                new PackageRegistrationAuditRecord(package, AuditedPackageRegistrationAction.RemoveOwner, user.Username));
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

            if (package.Deleted)
            {
                throw new InvalidOperationException("A deleted package should never be listed!");
            }
            if (!package.Listed && (package.IsLatestStable || package.IsLatest))
            {
                throw new InvalidOperationException("An unlisted package should never be latest or latest stable!");
            }

            package.Listed = true;
            package.LastUpdated = DateTime.UtcNow;
            package.LastEdited = DateTime.UtcNow;

            await UpdateIsLatestAsync(package.PackageRegistration, false);
            
            await _auditingService.SaveAuditRecord(new PackageAuditRecord(package, AuditedPackageAction.List));

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
            package.LastEdited = DateTime.UtcNow;

            if (package.IsLatest || package.IsLatestStable)
            {
                await UpdateIsLatestAsync(package.PackageRegistration, false);
            }

            await _auditingService.SaveAuditRecord(new PackageAuditRecord(package, AuditedPackageAction.Unlist));

            if (commitChanges)
            {
                await _packageRepository.CommitChangesAsync();
            }
        }

        public async Task<PackageOwnerRequest> CreatePackageOwnerRequestAsync(PackageRegistration package, User currentOwner, User newOwner)
        {
            var existingRequest = FindExistingPackageOwnerRequest(package, newOwner);
            if (existingRequest != null)
            {
                return existingRequest;
            }

            var newRequest = new PackageOwnerRequest
            {
                PackageRegistrationKey = package.Key,
                RequestingOwnerKey = currentOwner.Key,
                NewOwnerKey = newOwner.Key,
                ConfirmationCode = CryptographyService.GenerateToken(),
                RequestDate = DateTime.UtcNow
            };

            _packageOwnerRequestRepository.InsertOnCommit(newRequest);
            await _packageOwnerRequestRepository.CommitChangesAsync();
            return newRequest;
        }

        public async Task<ConfirmOwnershipResult> ConfirmPackageOwnerAsync(PackageRegistration package, User pendingOwner, string token)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (pendingOwner == null)
            {
                throw new ArgumentNullException(nameof(pendingOwner));
            }

            if (String.IsNullOrEmpty(token))
            {
                throw new ArgumentNullException(nameof(token));
            }

            if (package.IsOwner(pendingOwner))
            {
                return ConfirmOwnershipResult.AlreadyOwner;
            }

            var request = FindExistingPackageOwnerRequest(package, pendingOwner);
            if (request != null && request.ConfirmationCode == token)
            {
                await AddPackageOwnerAsync(package, pendingOwner);
                return ConfirmOwnershipResult.Success;
            }

            return ConfirmOwnershipResult.Failure;
        }

        private PackageRegistration CreateOrGetPackageRegistration(User currentUser, PackageMetadata packageMetadata)
        {
            var packageRegistration = FindPackageRegistrationById(packageMetadata.Id);

            if (packageRegistration != null && !packageRegistration.Owners.Contains(currentUser))
            {
                throw new EntityException(Strings.PackageIdNotAvailable, packageMetadata.Id);
            }

            if (packageRegistration == null)
            {
                if (_packageNamingConflictValidator.IdConflictsWithExistingPackageTitle(packageMetadata.Id))
                {
                    throw new EntityException(Strings.NewRegistrationIdMatchesExistingPackageTitle, packageMetadata.Id);
                }

                packageRegistration = new PackageRegistration
                {
                    Id = packageMetadata.Id
                };

                packageRegistration.Owners.Add(currentUser);

                _packageRegistrationRepository.InsertOnCommit(packageRegistration);
            }

            return packageRegistration;
        }

        private Package CreatePackageFromNuGetPackage(PackageRegistration packageRegistration, PackageArchiveReader nugetPackage, PackageMetadata packageMetadata, PackageStreamMetadata packageStreamMetadata, User user)
        {
            var package = packageRegistration.Packages.SingleOrDefault(pv => pv.Version == packageMetadata.Version.ToString());

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
            // Version must always be the exact string from the nuspec, which ToString will return to us.
            // However, we do also store a normalized copy for looking up later.
            package.Version = packageMetadata.Version.ToString();
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

#pragma warning disable 618 // TODO: remove Package.Authors completely once prodution services definitely no longer need it
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
            if (packageMetadata.Version.IsPrerelease)
            {
                var release = packageMetadata.Version.Release;

                if (release.Contains("."))
                {
                    throw new EntityException(Strings.NuGetPackageReleaseVersionWithDot, "Version");
                }

                long temp;
                if (long.TryParse(release, out temp))
                {
                    throw new EntityException(Strings.NuGetPackageReleaseVersionContainsOnlyNumerics, "Version");
                }
            }
            if (packageMetadata.Authors != null && packageMetadata.Authors.Flatten().Length > 4000)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Authors", "4000");
            }
            if (packageMetadata.Copyright != null && packageMetadata.Copyright.Length > 4000)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Copyright", "4000");
            }
            if (packageMetadata.Description != null && packageMetadata.Description.Length > 4000)
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

            if (packageMetadata.Version != null && packageMetadata.Version.ToString().Length > 64)
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

        public async Task UpdateIsLatestAsync(PackageRegistration packageRegistration, bool commitChanges = true)
        {
            if (!packageRegistration.Packages.Any())
            {
                return;
            }

            // TODO: improve setting the latest bit; this is horrible. Trigger maybe?
            foreach (var pv in packageRegistration.Packages.Where(p => p.IsLatest || p.IsLatestStable))
            {
                pv.IsLatest = false;
                pv.IsLatestStable = false;
                pv.LastUpdated = DateTime.UtcNow;
            }

            // If the last listed package was just unlisted, then we won't find another one
            var latestPackage = FindPackage(packageRegistration.Packages, p => !p.Deleted && p.Listed);

            if (latestPackage != null)
            {
                latestPackage.IsLatest = true;
                latestPackage.LastUpdated = DateTime.UtcNow;

                if (latestPackage.IsPrerelease)
                {
                    // If the newest uploaded package is a prerelease package, we need to find an older package that is
                    // a release version and set it to IsLatest.
                    var latestReleasePackage = FindPackage(packageRegistration.Packages.Where(p => !p.IsPrerelease && !p.Deleted && p.Listed));
                    if (latestReleasePackage != null)
                    {
                        // We could have no release packages
                        latestReleasePackage.IsLatestStable = true;
                        latestReleasePackage.LastUpdated = DateTime.UtcNow;
                    }
                }
                else
                {
                    // Only release versions are marked as IsLatestStable.
                    latestPackage.IsLatestStable = true;
                }
            }

            if (commitChanges)
            {
                await _packageRepository.CommitChangesAsync();
            }
        }

        private static Package FindPackage(IEnumerable<Package> packages, Func<Package, bool> predicate = null)
        {
            if (predicate != null)
            {
                packages = packages.Where(predicate);
            }
            NuGetVersion version = packages.Max(p => new NuGetVersion(p.Version));

            if (version == null)
            {
                return null;
            }
            return packages.First(pv => pv.Version.Equals(version.ToString(), StringComparison.OrdinalIgnoreCase));
        }

        private PackageOwnerRequest FindExistingPackageOwnerRequest(PackageRegistration package, User pendingOwner)
        {
            return (from request in _packageOwnerRequestRepository.GetAll()
                    where request.PackageRegistrationKey == package.Key && request.NewOwnerKey == pendingOwner.Key
                    select request).FirstOrDefault();
        }

        private void NotifyIndexingService()
        {
            _indexingService.UpdateIndex();
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
            var package = FindPackageByIdAndVersion(id, version);
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
    }
}
