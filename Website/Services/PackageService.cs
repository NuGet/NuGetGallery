using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Transactions;
using MvcMiniProfiler;
using NuGet;

namespace NuGetGallery
{
    public class PackageService : IPackageService
    {
        private readonly ICryptographyService cryptoSvc;
        private readonly IEntityRepository<PackageRegistration> packageRegistrationRepo;
        private readonly IEntityRepository<Package> packageRepo;
        private readonly IEntityRepository<PackageStatistics> packageStatsRepo;
        private readonly IPackageFileService packageFileSvc;
        private readonly IEntityRepository<PackageOwnerRequest> packageOwnerRequestRepository;
        private readonly IIndexingService indexingSvc;

        public PackageService(
            ICryptographyService cryptoSvc,
            IEntityRepository<PackageRegistration> packageRegistrationRepo,
            IEntityRepository<Package> packageRepo,
            IEntityRepository<PackageStatistics> packageStatsRepo,
            IPackageFileService packageFileSvc,
            IEntityRepository<PackageOwnerRequest> packageOwnerRequestRepository,
            IIndexingService indexingSvc)
        {
            this.cryptoSvc = cryptoSvc;
            this.packageRegistrationRepo = packageRegistrationRepo;
            this.packageRepo = packageRepo;
            this.packageStatsRepo = packageStatsRepo;
            this.packageFileSvc = packageFileSvc;
            this.packageOwnerRequestRepository = packageOwnerRequestRepository;
            this.indexingSvc = indexingSvc;
        }

        public Package CreatePackage(IPackage nugetPackage, User currentUser)
        {
            ValidateNuGetPackage(nugetPackage);

            var packageRegistration = CreateOrGetPackageRegistration(currentUser, nugetPackage);

            var package = CreatePackageFromNuGetPackage(packageRegistration, nugetPackage);
            packageRegistration.Packages.Add(package);

            using (var tx = new TransactionScope())
            {
                using (var stream = nugetPackage.GetStream())
                {
                    UpdateIsLatest(packageRegistration);
                    packageRegistrationRepo.CommitChanges();
                    packageFileSvc.SavePackageFile(package, stream);
                    tx.Complete();
                }
            }

            NotifyIndexingService();

            return package;
        }

        public void DeletePackage(string id, string version)
        {
            var package = FindPackageByIdAndVersion(id, version);

            if (package == null)
            {
                throw new EntityException(Strings.PackageWithIdAndVersionNotFound, id, version);
            }

            using (var tx = new TransactionScope())
            {
                var packageRegistration = package.PackageRegistration;
                packageRepo.DeleteOnCommit(package);
                packageFileSvc.DeletePackageFile(id, version);
                UpdateIsLatest(packageRegistration);
                packageRepo.CommitChanges();
                if (packageRegistration.Packages.Count == 0)
                {
                    packageRegistrationRepo.DeleteOnCommit(packageRegistration);
                    packageRegistrationRepo.CommitChanges();
                }
                tx.Complete();
            }

            NotifyIndexingService();
        }

        public virtual PackageRegistration FindPackageRegistrationById(string id)
        {
            return packageRegistrationRepo.GetAll()
                .Include(pr => pr.Owners)
                .Where(pr => pr.Id == id)
                .SingleOrDefault();
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
            IEnumerable<Package> packagesQuery = packageRepo.GetAll()
                                                            .Include(p => p.Authors)
                                                            .Include(p => p.PackageRegistration)
                                                            .Where(p => (p.PackageRegistration.Id == id));
            if (String.IsNullOrEmpty(version) && !allowPrerelease) 
            {
                // If there's a specific version given, don't bother filtering by prerelease. You could be asking for a prerelease package.
                packagesQuery = packagesQuery.Where(p => !p.IsPrerelease);
            }
            var packageVersions = packagesQuery.ToList();

            Package package = null;
            if (version == null)
            {
                if (allowPrerelease)
                {
                    package = packageVersions.FirstOrDefault(p => p.IsLatest);
                }
                else
                {
                    package = packageVersions.FirstOrDefault(p => p.IsLatestStable);
                }

                // If we couldn't find a package marked as latest, then
                // return the most recent one.
                if (package == null)
                {
                    package = packageVersions.OrderByDescending(p => p.Version).FirstOrDefault();
                }
            }
            else
            {
                package = packageVersions
                    .Where(p => p.PackageRegistration.Id.Equals(id, StringComparison.OrdinalIgnoreCase) && p.Version.Equals(version, StringComparison.OrdinalIgnoreCase))
                    .SingleOrDefault();
            }
            return package;
        }

        public IQueryable<Package> GetLatestPackageVersions(bool allowPrerelease)
        {
            var packages = packageRepo.GetAll()
                .Include(x => x.PackageRegistration)
                .Include(x => x.Authors)
                .Include(x => x.PackageRegistration.Owners);

            if (allowPrerelease)
            {
                // Since we use this for listing, only show prerelease versions of a package if it does not have stable version 
                return packages.Where(p => p.IsLatestStable || (p.IsLatest && !p.PackageRegistration.Packages.Any(p2 => p2.IsLatestStable)));
            }
            return packages.Where(x => x.IsLatestStable);
        }

        public IEnumerable<Package> FindPackagesByOwner(User user)
        {
            return (from pr in packageRegistrationRepo.GetAll()
                    from u in pr.Owners
                    where u.Username == user.Username
                    from p in pr.Packages
                    select p).Include(p => p.PackageRegistration).ToList();
        }

        public IEnumerable<Package> FindDependentPackages(Package package)
        {
            // Grab all candidates
            var candidateDependents = (from p in packageRepo.GetAll()
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

        public void PublishPackage(string id, string version)
        {
            var package = FindPackageByIdAndVersion(id, version);

            if (package == null)
                throw new EntityException(Strings.PackageWithIdAndVersionNotFound, id, version);

            package.Published = DateTime.UtcNow;
            package.Listed = true;

            UpdateIsLatest(package.PackageRegistration);

            packageRepo.CommitChanges();
        }

        public void AddDownloadStatistics(Package package, string userHostAddress, string userAgent)
        {
            using (MiniProfiler.Current.Step("Updating package stats"))
            {
                packageStatsRepo.InsertOnCommit(new PackageStatistics
                {
                    // IMPORTANT: Timestamp is managed by the database.

                    // IMPORTANT: Until we understand privacy implications of storing IP Addresses thoroughly,
                    // It's better to just not store them. Hence "unknown". - Phil Haack 10/6/2011
                    IPAddress = "unknown",
                    UserAgent = userAgent,
                    Package = package
                });

                packageStatsRepo.CommitChanges();
            }
        }

        PackageRegistration CreateOrGetPackageRegistration(User currentUser, IPackage nugetPackage)
        {
            var packageRegistration = FindPackageRegistrationById(nugetPackage.Id);

            if (packageRegistration != null && !packageRegistration.Owners.Contains(currentUser))
                throw new EntityException(Strings.PackageIdNotAvailable, nugetPackage.Id);

            if (packageRegistration == null)
            {
                packageRegistration = new PackageRegistration
                {
                    Id = nugetPackage.Id
                };

                packageRegistration.Owners.Add(currentUser);

                packageRegistrationRepo.InsertOnCommit(packageRegistration);
            }

            return packageRegistration;
        }

        Package CreatePackageFromNuGetPackage(PackageRegistration packageRegistration, IPackage nugetPackage)
        {
            var package = packageRegistration.Packages
                .Where(pv => pv.Version == nugetPackage.Version.ToString())
                .SingleOrDefault();

            if (package != null)
                throw new EntityException("A package with identifier '{0}' and version '{1}' already exists.", packageRegistration.Id, package.Version);

            // TODO: add flattened authors, and other properties
            // TODO: add package size
            var now = DateTime.UtcNow;
            var packageFileStream = nugetPackage.GetStream();

            package = new Package
            {
                Version = nugetPackage.Version.ToString(),
                Description = nugetPackage.Description,
                ReleaseNotes = nugetPackage.ReleaseNotes,
                RequiresLicenseAcceptance = nugetPackage.RequireLicenseAcceptance,
                HashAlgorithm = Constants.Sha512HashAlgorithmId,
                Hash = cryptoSvc.GenerateHash(packageFileStream.ReadAllBytes()),
                PackageFileSize = packageFileStream.Length,
                Created = now,
                LastUpdated = now,
                Published = now,
                Copyright = nugetPackage.Copyright,
                IsPrerelease = !nugetPackage.IsReleaseVersion(),
                Listed = true
            };

            if (nugetPackage.IconUrl != null)
                package.IconUrl = nugetPackage.IconUrl.ToString();
            if (nugetPackage.LicenseUrl != null)
                package.LicenseUrl = nugetPackage.LicenseUrl.ToString();
            if (nugetPackage.ProjectUrl != null)
                package.ProjectUrl = nugetPackage.ProjectUrl.ToString();
            if (nugetPackage.Summary != null)
                package.Summary = nugetPackage.Summary;
            if (nugetPackage.Tags != null)
                package.Tags = nugetPackage.Tags;
            if (nugetPackage.Title != null)
                package.Title = nugetPackage.Title;

            foreach (var author in nugetPackage.Authors)
                package.Authors.Add(new PackageAuthor { Name = author });

            foreach (var dependency in nugetPackage.Dependencies)
                package.Dependencies.Add(new PackageDependency { Id = dependency.Id, VersionSpec = dependency.VersionSpec.ToStringSafe() });

            package.FlattenedAuthors = package.Authors.Flatten();
            package.FlattenedDependencies = package.Dependencies.Flatten();

            return package;
        }

        static void ValidateNuGetPackage(IPackage nugetPackage)
        {
            if (nugetPackage.Id.Length > 128)
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Id", "128");
            if (nugetPackage.Authors != null && String.Join(",", nugetPackage.Authors.ToArray()).Length > 4000)
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Authors", "4000");
            if (nugetPackage.Copyright != null && nugetPackage.Copyright.Length > 4000)
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Copyright", "4000");
            if (nugetPackage.Dependencies != null && nugetPackage.Dependencies.Flatten().Length > 4000)
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Dependencies", "4000");
            if (nugetPackage.Description != null && nugetPackage.Description.Length > 4000)
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Description", "4000");
            if (nugetPackage.IconUrl != null && nugetPackage.IconUrl.ToString().Length > 4000)
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "IconUrl", "4000");
            if (nugetPackage.LicenseUrl != null && nugetPackage.LicenseUrl.ToString().Length > 4000)
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "LicenseUrl", "4000");
            if (nugetPackage.ProjectUrl != null && nugetPackage.ProjectUrl.ToString().Length > 4000)
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "ProjectUrl", "4000");
            if (nugetPackage.Summary != null && nugetPackage.Summary.ToString().Length > 4000)
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Summary", "4000");
            if (nugetPackage.Tags != null && nugetPackage.Tags.ToString().Length > 4000)
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Tags", "4000");
            if (nugetPackage.Title != null && nugetPackage.Title.ToString().Length > 4000)
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Title", "4000");
        }

        private static void UpdateIsLatest(PackageRegistration packageRegistration)
        {
            if (!packageRegistration.Packages.Any())
            {
                return;
            }

            // TODO: improve setting the latest bit; this is horrible. Trigger maybe?
            // NOTE: EF is suprisingly smart about doing this. It doesn't issue queries for the vast majority of packages that did not have either flags changed.
            foreach (var pv in packageRegistration.Packages)
            {
                pv.IsLatest = false;
                pv.IsLatestStable = false;
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

        public void AddPackageOwner(PackageRegistration package, User user)
        {
            package.Owners.Add(user);
            packageRepo.CommitChanges();

            var request = FindExistingPackageOwnerRequest(package, user);
            if (request != null)
            {
                packageOwnerRequestRepository.DeleteOnCommit(request);
                packageOwnerRequestRepository.CommitChanges();
            }
        }

        public void RemovePackageOwner(PackageRegistration package, User user)
        {
            var pendingOwner = FindExistingPackageOwnerRequest(package, user);
            if (pendingOwner != null)
            {
                packageOwnerRequestRepository.DeleteOnCommit(pendingOwner);
                packageOwnerRequestRepository.CommitChanges();
                return;
            }

            package.Owners.Remove(user);
            packageRepo.CommitChanges();
        }

        // TODO: Should probably be run in a transaction
        public void MarkPackageListed(Package package)
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

            UpdateIsLatest(package.PackageRegistration);

            packageRepo.CommitChanges();
        }

        // TODO: Should probably be run in a transaction
        public void MarkPackageUnlisted(Package package)
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

            if (package.IsLatest || package.IsLatestStable)
            {
                UpdateIsLatest(package.PackageRegistration);
            }
            packageRepo.CommitChanges();
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
                ConfirmationCode = cryptoSvc.GenerateToken(),
                RequestDate = DateTime.UtcNow
            };

            packageOwnerRequestRepository.InsertOnCommit(newRequest);
            packageOwnerRequestRepository.CommitChanges();
            return newRequest;
        }

        public bool ConfirmPackageOwner(PackageRegistration package, User pendingOwner, string token)
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
                return true;
            }

            var request = FindExistingPackageOwnerRequest(package, pendingOwner);
            if (request != null && request.ConfirmationCode == token)
            {
                AddPackageOwner(package, pendingOwner);
                return true;
            }

            return false;
        }

        private PackageOwnerRequest FindExistingPackageOwnerRequest(PackageRegistration package, User pendingOwner)
        {
            return (from request in packageOwnerRequestRepository.GetAll()
                    where request.PackageRegistrationKey == package.Key && request.NewOwnerKey == pendingOwner.Key
                    select request).FirstOrDefault();
        }

        private void NotifyIndexingService()
        {
            indexingSvc.UpdateIndex();
        }
    }
}