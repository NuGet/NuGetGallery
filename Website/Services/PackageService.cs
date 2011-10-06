using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Transactions;
using MvcMiniProfiler;
using NuGet;

namespace NuGetGallery {
    public class PackageService : IPackageService {
        readonly ICryptographyService cryptoSvc;
        readonly IEntityRepository<PackageRegistration> packageRegistrationRepo;
        readonly IEntityRepository<Package> packageRepo;
        readonly IEntityRepository<PackageStatistics> packageStatsRepo;
        readonly IPackageFileService packageFileSvc;

        public PackageService(
            ICryptographyService cryptoSvc,
            IEntityRepository<PackageRegistration> packageRegistrationRepo,
            IEntityRepository<Package> packageRepo,
            IEntityRepository<PackageStatistics> packageStatsRepo,
            IPackageFileService packageFileSvc) {
            this.cryptoSvc = cryptoSvc;
            this.packageRegistrationRepo = packageRegistrationRepo;
            this.packageRepo = packageRepo;
            this.packageStatsRepo = packageStatsRepo;
            this.packageFileSvc = packageFileSvc;
        }

        public Package CreatePackage(IPackage nugetPackage, User currentUser) {
            ValidateNuGetPackage(nugetPackage);

            var packageRegistration = CreateOrGetPackageRegistration(currentUser, nugetPackage);

            var package = CreatePackageFromNuGetPackage(packageRegistration, nugetPackage);
            UpdatePackageListed(package);
            packageRegistration.Packages.Add(package);

            using (var tx = new TransactionScope())
            using (var stream = nugetPackage.GetStream()) {
                packageRegistrationRepo.CommitChanges();
                packageFileSvc.SavePackageFile(package, stream);
                tx.Complete();
            }

            return package;
        }

        public void DeletePackage(string id, string version) {
            var package = FindPackageByIdAndVersion(id, version);

            if (package == null) {
                throw new EntityException(Strings.PackageWithIdAndVersionNotFound, id, version);
            }

            using (var tx = new TransactionScope()) {
                var packageRegistration = package.PackageRegistration;
                packageRepo.DeleteOnCommit(package);
                packageFileSvc.DeletePackageFile(id, version);
                UpdateIsLatest(packageRegistration);
                packageRepo.CommitChanges();
                if (packageRegistration.Packages.Count == 0) {
                    packageRegistrationRepo.DeleteOnCommit(packageRegistration);
                    packageRegistrationRepo.CommitChanges();
                }
                tx.Complete();
            }
        }

        public virtual PackageRegistration FindPackageRegistrationById(string id) {
            return packageRegistrationRepo.GetAll()
                .Include(pr => pr.Owners)
                .Where(pr => pr.Id == id)
                .SingleOrDefault();
        }

        public virtual Package FindPackageByIdAndVersion(string id, string version, bool allowPrerelease = true) {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException("id");

            // Optimization: Everytime we look at a package we almost always want to see 
            // all the other packages with the same via the PackageRegistration property. 
            // This resulted in a gnarly query. 
            // Instead, we can always query for all packages with the ID and then fix up 
            // the Packages property for the one we plan to return.
            var packageVersions = packageRepo.GetAll()
                    .Include(p => p.Authors)
                    .Include(p => p.PackageRegistration)
                    .Where(p => (p.PackageRegistration.Id == id) && (allowPrerelease || !p.IsPrerelease))
                    .ToList();

            Package package = null;
            if (version == null) {
                if (allowPrerelease) {
                    package = packageVersions.FirstOrDefault(p => p.IsLatest);
                }
                else {
                    package = packageVersions.FirstOrDefault(p => p.IsLatestStable);
                }
                
                if (package == null) {
                    throw new InvalidOperationException("Packages are in a bad state. At least one should have IsLatest or IsAbsoluteLatest set");
                }
            }
            else {
                package = packageVersions
                    .Where(p => p.PackageRegistration.Id == id && p.Version == version)
                    .SingleOrDefault();
            }
            if (package != null) {
                package.PackageRegistration.Packages = packageVersions;
            }
            return package;
        }

        public IQueryable<Package> GetLatestPackageVersions(bool allowPrerelease) {
            var packages =  packageRepo.GetAll()
                .Include(x => x.PackageRegistration)
                .Include(x => x.Authors)
                .Include(x => x.PackageRegistration.Owners);

            if (allowPrerelease) {
                // Since we use this for listing, when we allow pre release versions, we'll assume they meant to show both the latest release and prerelease versions of a package.
                return packages.Where(p => p.IsLatest || p.IsLatestStable);
            }
            return packages.Where(x => x.IsLatestStable);
        }

        public IEnumerable<Package> FindPackagesByOwner(User user) {
            return (from pr in packageRegistrationRepo.GetAll()
                    from u in pr.Owners
                    where u.Username == user.Username
                    from p in pr.Packages
                    select p).Include(p => p.PackageRegistration).ToList();
        }

        public IEnumerable<Package> FindDependentPackages(Package package) {
            // Grab all candidates
            var candidateDependents = (from p in packageRepo.GetAll()
                                       from d in p.Dependencies
                                       where d.Id == package.PackageRegistration.Id
                                       select d).Include(pk => pk.Package.PackageRegistration).ToList();
            // Now filter by version range.
            var packageVersion = new SemanticVersion(package.Version);
            var dependents = from d in candidateDependents
                             where VersionUtility.ParseVersionSpec(d.VersionRange).Satisfies(packageVersion)
                             select d;

            return dependents.Select(d => d.Package);
        }

        public void PublishPackage(string id, string version) {
            var package = FindPackageByIdAndVersion(id, version);

            if (package == null)
                throw new EntityException(Strings.PackageWithIdAndVersionNotFound, id, version);

            package.Published = DateTime.UtcNow;

            UpdateIsLatest(package.PackageRegistration);

            packageRepo.CommitChanges();
        }

        public void AddDownloadStatistics(Package package, string userHostAddress, string userAgent) {
            using (MiniProfiler.Current.Step("Updating package stats")) {
                packageStatsRepo.InsertOnCommit(new PackageStatistics {
                    Timestamp = DateTime.UtcNow,
                    IPAddress = userHostAddress,
                    UserAgent = userAgent,
                    Package = package
                });

                packageStatsRepo.CommitChanges();
            }
        }

        PackageRegistration CreateOrGetPackageRegistration(User currentUser, IPackage nugetPackage) {
            var packageRegistration = FindPackageRegistrationById(nugetPackage.Id);

            if (packageRegistration != null && !packageRegistration.Owners.Contains(currentUser))
                throw new EntityException(Strings.PackageIdNotAvailable, nugetPackage.Id);

            if (packageRegistration == null) {
                packageRegistration = new PackageRegistration {
                    Id = nugetPackage.Id
                };

                packageRegistration.Owners.Add(currentUser);

                packageRegistrationRepo.InsertOnCommit(packageRegistration);
            }

            return packageRegistration;
        }

        Package CreatePackageFromNuGetPackage(PackageRegistration packageRegistration, IPackage nugetPackage) {
            var package = packageRegistration.Packages
                .Where(pv => pv.Version == nugetPackage.Version.ToString())
                .SingleOrDefault();

            if (package != null)
                throw new EntityException("A package with identifier '{0}' and version '{1}' already exists.", packageRegistration.Id, package.Version);

            // TODO: add flattened authors, and other properties
            // TODO: add package size
            var now = DateTime.UtcNow;
            var packageFileStream = nugetPackage.GetStream();

            package = new Package {
                Version = nugetPackage.Version.ToString(),
                Description = nugetPackage.Description,
                RequiresLicenseAcceptance = nugetPackage.RequireLicenseAcceptance,
                HashAlgorithm = cryptoSvc.HashAlgorithmId,
                Hash = cryptoSvc.GenerateHash(packageFileStream.ReadAllBytes()),
                PackageFileSize = packageFileStream.Length,
                Created = now,
                LastUpdated = now,
                Published = DateTime.UtcNow,
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
                package.Dependencies.Add(new PackageDependency { Id = dependency.Id, VersionRange = dependency.VersionSpec.ToStringSafe() });

            package.FlattenedAuthors = package.Authors.Flatten();
            package.FlattenedDependencies = package.Dependencies.Flatten();

            return package;
        }

        static void ValidateNuGetPackage(IPackage nugetPackage) {
            if (nugetPackage.Id.Length > 128)
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Id", "128");
            if (nugetPackage.Authors != null && string.Join(",", nugetPackage.Authors.ToArray()).Length > 4000)
                throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Authors", "4000");
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

        void UpdateIsLatest(PackageRegistration packageRegistration) {
            if (!packageRegistration.Packages.Any()) {
                return;
            }

            // TODO: improve setting the latest bit; this is horrible. Trigger maybe?
            foreach (var pv in packageRegistration.Packages) {
                pv.IsLatest = false;
                pv.IsLatestStable = false;
            }

            var latestPackage = FindPackage(packageRegistration.Packages, null);
            latestPackage.IsLatest = true;

            if (latestPackage.IsPrerelease) {
                // If the newest uploaded package is a prerelease package, we need to find an older package that is 
                // a release version and set it to IsLatest.
                var latestReleasePackage = FindPackage(packageRegistration.Packages.Where(p => !p.IsPrerelease));
                if (latestReleasePackage != null) {
                    // We could have no release packages
                    latestReleasePackage.IsLatestStable = true;
                }
            }
            else {
                // Only release versions are marked as IsLatestStable. 
                latestPackage.IsLatestStable = true;
            }
        }

        public void AddPackageOwner(Package package, User user) {
            package.PackageRegistration.Owners.Add(user);
            packageRepo.CommitChanges();
        }

        public void RemovePackageOwner(Package package, User user) {
            package.PackageRegistration.Owners.Remove(user);
            packageRepo.CommitChanges();
        }

        public void MarkPackageListed(Package package) {
            foreach (var item in package.PackageRegistration.Packages) {
                item.Listed = true;
            }
            packageRepo.CommitChanges();
        }

        public void MarkPackageUnlisted(Package package) {
            foreach (var item in package.PackageRegistration.Packages) {
                item.Listed = false;
            }
            packageRepo.CommitChanges();
        }

        private static Package FindPackage(IEnumerable<Package> packages, Func<Package, bool> predicate = null) {
            if (predicate != null) {
                packages = packages.Where(predicate);
            }
            SemanticVersion version = packages.Max(p => new SemanticVersion(p.Version));

            if (version == null) {
                return null;
            }
            return packages.First(pv => pv.Version.Equals(version.ToString(), StringComparison.OrdinalIgnoreCase));
        }


        private void UpdatePackageListed(Package package) {
            var packages = packageRepo.GetAll().Where(p => p.PackageRegistration.Id == p.PackageRegistration.Id);

        }
    }
}