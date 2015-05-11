// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;
using NuGetGallery.Packaging;
using Crypto = NuGetGallery.CryptographyService;

namespace NuGetGallery
{
    public class PackageService : IPackageService
    {
        private readonly IIndexingService _indexingService;
        private readonly IEntityRepository<PackageOwnerRequest> _packageOwnerRequestRepository;
        private readonly IEntityRepository<PackageRegistration> _packageRegistrationRepository;
        private readonly IEntityRepository<Package> _packageRepository;
        private readonly IEntityRepository<PackageStatistics> _packageStatsRepository;

        public PackageService(
            IEntityRepository<PackageRegistration> packageRegistrationRepository,
            IEntityRepository<Package> packageRepository,
            IEntityRepository<PackageStatistics> packageStatsRepository,
            IEntityRepository<PackageOwnerRequest> packageOwnerRequestRepository,
            IIndexingService indexingService)
        {
            _packageRegistrationRepository = packageRegistrationRepository;
            _packageRepository = packageRepository;
            _packageStatsRepository = packageStatsRepository;
            _packageOwnerRequestRepository = packageOwnerRequestRepository;
            _indexingService = indexingService;
        }

        public Package CreatePackage(INupkg nugetPackage, User user, bool commitChanges = true)
        {
            ValidateNuGetPackageMetadata(nugetPackage.Metadata);

            var packageRegistration = CreateOrGetPackageRegistration(user, nugetPackage.Metadata);

            var package = CreatePackageFromNuGetPackage(packageRegistration, nugetPackage, user);
            packageRegistration.Packages.Add(package);
            UpdateIsLatest(packageRegistration);

            if (commitChanges)
            {
                _packageRegistrationRepository.CommitChanges();
                NotifyIndexingService();
            }

            return package;
        }

        public void DeletePackage(string id, string version, bool commitChanges = true)
        {
            var package = FindPackageByIdAndVersion(id, version);

            if (package == null)
            {
                throw new EntityException(Strings.PackageWithIdAndVersionNotFound, id, version);
            }

            var packageRegistration = package.PackageRegistration;
            _packageRepository.DeleteOnCommit(package);

            UpdateIsLatest(packageRegistration);

            if (packageRegistration.Packages.Count == 0)
            {
                _packageRegistrationRepository.DeleteOnCommit(packageRegistration);
            }

            if (commitChanges)
            {
                // we don't need to call _packageRegistrationRepository.CommitChanges() here because 
                // it shares the same EntityContext as _packageRepository.
                _packageRepository.CommitChanges();

                NotifyIndexingService();
            }
        }

        public virtual PackageRegistration FindPackageRegistrationById(string id)
        {
            if (id == null)
            {
                throw new ArgumentNullException("id");
            }

            return _packageRegistrationRepository.GetAll()
                .Include(pr => pr.Owners)
                .SingleOrDefault(pr => pr.Id == id);
        }

        public virtual Package FindPackageByIdAndVersion(string id, string version, bool allowPrerelease = true)
        {
            if (String.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException("id");
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
                            String.Equals(p.NormalizedVersion, SemanticVersionExtensions.Normalize(version), StringComparison.OrdinalIgnoreCase)
                         ));
            }
            return package;
        }

        public IQueryable<Package> GetPackagesForListing(bool includePrerelease)
        {
            var packages = _packageRepository.GetAll()
                .Include(x => x.PackageRegistration)
                .Include(x => x.PackageRegistration.Owners)
                .Where(p => p.Listed);

            return includePrerelease
                       ? packages.Where(p => p.IsLatest)
                       : packages.Where(p => p.IsLatestStable);
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
                mergedResults.Add(package.PackageRegistration.Id, package);
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
            var packageVersion = new SemanticVersion(package.Version);
            var dependents = from d in candidateDependents
                             where VersionUtility.ParseVersionSpec(d.VersionSpec).Satisfies(packageVersion)
                             select d;

            return dependents.Select(d => d.Package);
        }

        public void PublishPackage(string id, string version, bool commitChanges = true)
        {
            var package = FindPackageByIdAndVersion(id, version);

            if (package == null)
            {
                throw new EntityException(Strings.PackageWithIdAndVersionNotFound, id, version);
            }

            PublishPackage(package, commitChanges);
        }

        public void PublishPackage(Package package, bool commitChanges = true)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            package.Published = DateTime.UtcNow;
            package.Listed = true;

            UpdateIsLatest(package.PackageRegistration);

            if (commitChanges)
            {
                _packageRepository.CommitChanges();
            }
        }

        public void AddDownloadStatistics(PackageStatistics stats)
        {
            // IMPORTANT: Until we understand privacy implications of storing IP Addresses thoroughly,
            // It's better to just not store them. Hence "unknown". - Phil Haack 10/6/2011
            stats.IPAddress = "unknown";
            _packageStatsRepository.InsertOnCommit(stats);
            _packageStatsRepository.CommitChanges();
        }

        public void AddPackageOwner(PackageRegistration package, User user)
        {
            package.Owners.Add(user);
            _packageRepository.CommitChanges();

            var request = FindExistingPackageOwnerRequest(package, user);
            if (request != null)
            {
                _packageOwnerRequestRepository.DeleteOnCommit(request);
                _packageOwnerRequestRepository.CommitChanges();
            }
        }

        public void RemovePackageOwner(PackageRegistration package, User user)
        {
            if (package.Owners.Count == 1 && user == package.Owners.Single())
            {
                throw new InvalidOperationException("You can't remove the only owner from a package.");
            }

            var pendingOwner = FindExistingPackageOwnerRequest(package, user);
            if (pendingOwner != null)
            {
                _packageOwnerRequestRepository.DeleteOnCommit(pendingOwner);
                _packageOwnerRequestRepository.CommitChanges();
                return;
            }

            package.Owners.Remove(user);
            _packageRepository.CommitChanges();
        }

        public void MarkPackageListed(Package package, bool commitChanges = true)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            if (package.Listed)
            {
                return;
            }

            if (!package.Listed && (package.IsLatestStable || package.IsLatest))
            {
                throw new InvalidOperationException("An unlisted package should never be latest or latest stable!");
            }

            package.Listed = true;
            package.LastUpdated = DateTime.UtcNow;
            package.LastEdited = DateTime.UtcNow;

            UpdateIsLatest(package.PackageRegistration);

            if (commitChanges)
            {
                _packageRepository.CommitChanges();
            }
        }

        public void MarkPackageUnlisted(Package package, bool commitChanges = true)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
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
                UpdateIsLatest(package.PackageRegistration);
            }

            if (commitChanges)
            {
                _packageRepository.CommitChanges();
            }
        }

        public PackageOwnerRequest CreatePackageOwnerRequest(PackageRegistration package, User currentOwner, User newOwner)
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
                    ConfirmationCode = Crypto.GenerateToken(),
                    RequestDate = DateTime.UtcNow
                };

            _packageOwnerRequestRepository.InsertOnCommit(newRequest);
            _packageOwnerRequestRepository.CommitChanges();
            return newRequest;
        }

        public ConfirmOwnershipResult ConfirmPackageOwner(PackageRegistration package, User pendingOwner, string token)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            if (pendingOwner == null)
            {
                throw new ArgumentNullException("pendingOwner");
            }

            if (String.IsNullOrEmpty(token))
            {
                throw new ArgumentNullException("token");
            }

            if (package.IsOwner(pendingOwner))
            {
                return ConfirmOwnershipResult.AlreadyOwner;
            }

            var request = FindExistingPackageOwnerRequest(package, pendingOwner);
            if (request != null && request.ConfirmationCode == token)
            {
                AddPackageOwner(package, pendingOwner);
                return ConfirmOwnershipResult.Success;
            }

            return ConfirmOwnershipResult.Failure;
        }

        private PackageRegistration CreateOrGetPackageRegistration(User currentUser, IPackageMetadata nugetPackage)
        {
            var packageRegistration = FindPackageRegistrationById(nugetPackage.Id);

            if (packageRegistration != null && !packageRegistration.Owners.Contains(currentUser))
            {
                throw new EntityException(Strings.PackageIdNotAvailable, nugetPackage.Id);
            }

            if (packageRegistration == null)
            {
                packageRegistration = new PackageRegistration
                    {
                        Id = nugetPackage.Id
                    };

                packageRegistration.Owners.Add(currentUser);

                _packageRegistrationRepository.InsertOnCommit(packageRegistration);
            }

            return packageRegistration;
        }

        private Package CreatePackageFromNuGetPackage(PackageRegistration packageRegistration, INupkg nugetPackage, User user)
        {
            var package = packageRegistration.Packages.SingleOrDefault(pv => pv.Version == nugetPackage.Metadata.Version.ToString());

            if (package != null)
            {
                throw new EntityException(
                    "A package with identifier '{0}' and version '{1}' already exists.", packageRegistration.Id, package.Version);
            }

            var now = DateTime.UtcNow;
            var packageFileStream = nugetPackage.GetStream();

            package = new Package
            {
                // Version must always be the exact string from the nuspec, which ToString will return to us. 
                // However, we do also store a normalized copy for looking up later.
                Version = nugetPackage.Metadata.Version.ToString(),
                NormalizedVersion = nugetPackage.Metadata.Version.ToNormalizedString(),

                Description = nugetPackage.Metadata.Description,
                ReleaseNotes = nugetPackage.Metadata.ReleaseNotes,
                HashAlgorithm = Constants.Sha512HashAlgorithmId,
                Hash = Crypto.GenerateHash(packageFileStream.ReadAllBytes()),
                PackageFileSize = packageFileStream.Length,
                Created = now,
                Language = nugetPackage.Metadata.Language,
                LastUpdated = now,
                Published = now,
                Copyright = nugetPackage.Metadata.Copyright,
                FlattenedAuthors = nugetPackage.Metadata.Authors.Flatten(),
                IsPrerelease = !nugetPackage.Metadata.IsReleaseVersion(),
                Listed = true,
                PackageRegistration = packageRegistration,
                RequiresLicenseAcceptance = nugetPackage.Metadata.RequireLicenseAcceptance,
                Summary = nugetPackage.Metadata.Summary,
                Tags = PackageHelper.ParseTags(nugetPackage.Metadata.Tags),
                Title = nugetPackage.Metadata.Title,
                User = user,
            };

            package.IconUrl = nugetPackage.Metadata.IconUrl.ToEncodedUrlStringOrNull();
            package.LicenseUrl = nugetPackage.Metadata.LicenseUrl.ToEncodedUrlStringOrNull();
            package.ProjectUrl = nugetPackage.Metadata.ProjectUrl.ToEncodedUrlStringOrNull();
            package.MinClientVersion = nugetPackage.Metadata.MinClientVersion.ToStringOrNull();

#pragma warning disable 618 // TODO: remove Package.Authors completely once prodution services definitely no longer need it
            foreach (var author in nugetPackage.Metadata.Authors)
            {
                package.Authors.Add(new PackageAuthor { Name = author });
            }
#pragma warning restore 618

            var supportedFrameworks = GetSupportedFrameworks(nugetPackage).Select(fn => fn.ToShortNameOrNull()).ToArray();
            if (!supportedFrameworks.AnySafe(sf => sf == null))
            {
                foreach (var supportedFramework in supportedFrameworks)
                {
                    package.SupportedFrameworks.Add(new PackageFramework { TargetFramework = supportedFramework });
                }
            }

            foreach (var dependencySet in nugetPackage.Metadata.DependencySets)
            {
                if (dependencySet.Dependencies.Count == 0)
                {
                    package.Dependencies.Add(
                        new PackageDependency
                            {
                                Id = null,
                                VersionSpec = null,
                                TargetFramework = dependencySet.TargetFramework.ToShortNameOrNull()
                            });
                }
                else
                {
                    foreach (var dependency in dependencySet.Dependencies.Select(d => new { d.Id, d.VersionSpec, dependencySet.TargetFramework }))
                    {
                        package.Dependencies.Add(
                            new PackageDependency
                                {
                                    Id = dependency.Id,
                                    VersionSpec = dependency.VersionSpec == null ? null : dependency.VersionSpec.ToString(),
                                    TargetFramework = dependency.TargetFramework.ToShortNameOrNull()
                                });
                    }
                }
            }

            package.FlattenedDependencies = package.Dependencies.Flatten();

            return package;
        }

        public virtual IEnumerable<FrameworkName> GetSupportedFrameworks(INupkg package)
        {
            return package.GetSupportedFrameworks();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private static void ValidateNuGetPackageMetadata(IPackageMetadata nugetPackage)
        {
            // TODO: Change this to use DataAnnotations
            if (nugetPackage.Id.Length > 128)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Id", "128");
            }
            if (nugetPackage.Authors != null && nugetPackage.Authors.Flatten().Length > 4000)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Authors", "4000");
            }
            if (nugetPackage.Copyright != null && nugetPackage.Copyright.Length > 4000)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Copyright", "4000");
            }
            if (nugetPackage.Description != null && nugetPackage.Description.Length > 4000)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Description", "4000");
            }
            if (nugetPackage.IconUrl != null && nugetPackage.IconUrl.AbsoluteUri.Length > 4000)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "IconUrl", "4000");
            }
            if (nugetPackage.LicenseUrl != null && nugetPackage.LicenseUrl.AbsoluteUri.Length > 4000)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "LicenseUrl", "4000");
            }
            if (nugetPackage.ProjectUrl != null && nugetPackage.ProjectUrl.AbsoluteUri.Length > 4000)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "ProjectUrl", "4000");
            }
            if (nugetPackage.Summary != null && nugetPackage.Summary.Length > 4000)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Summary", "4000");
            }
            if (nugetPackage.Tags != null && nugetPackage.Tags.Length > 4000)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Tags", "4000");
            }
            if (nugetPackage.Title != null && nugetPackage.Title.Length > 256)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Title", "256");
            }

            if (nugetPackage.Version != null && nugetPackage.Version.ToString().Length > 64)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Version", "64");
            }

            if (nugetPackage.Language != null && nugetPackage.Language.Length > 20)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Language", "20");
            }

            foreach (var dependency in nugetPackage.DependencySets.SelectMany(s => s.Dependencies))
            {
                if (dependency.Id != null && dependency.Id.Length > 128)
                {
                    throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Dependency.Id", "128");
                }

                if (dependency.VersionSpec != null && dependency.VersionSpec.ToString().Length > 256)
                {
                    throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Dependency.VersionSpec", "256");
                }
            }

            if (nugetPackage.DependencySets != null && nugetPackage.DependencySets.Flatten().Length > Int16.MaxValue)
            {
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Dependencies", Int16.MaxValue);
            }
        }

        private static void UpdateIsLatest(PackageRegistration packageRegistration)
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
            var latestPackage = FindPackage(packageRegistration.Packages, p => p.Listed);

            if (latestPackage != null)
            {
                latestPackage.IsLatest = true;
                latestPackage.LastUpdated = DateTime.UtcNow;

                if (latestPackage.IsPrerelease)
                {
                    // If the newest uploaded package is a prerelease package, we need to find an older package that is 
                    // a release version and set it to IsLatest.
                    var latestReleasePackage = FindPackage(packageRegistration.Packages.Where(p => !p.IsPrerelease && p.Listed));
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
        }

        private static Package FindPackage(IEnumerable<Package> packages, Func<Package, bool> predicate = null)
        {
            if (predicate != null)
            {
                packages = packages.Where(predicate);
            }
            SemanticVersion version = packages.Max(p => new SemanticVersion(p.Version));

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


        public void SetLicenseReportVisibility(Package package, bool visible, bool commitChanges = true)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }
            package.HideLicenseReport = !visible;
            if (commitChanges)
            {
                _packageRepository.CommitChanges();
            }
            _packageRepository.CommitChanges();
        }
    }
}
