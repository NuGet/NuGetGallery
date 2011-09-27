using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Transactions;
using MvcMiniProfiler;
using NuGet;
using System.IO;

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
            packageRegistration.Packages.Add(package);

            using (var tx = new TransactionScope())
            using (var stream = nugetPackage.GetStream()) {
                packageRegistrationRepo.CommitChanges();
                SavePackageFile(package,stream);
                tx.Complete();
            }

            return package;
        }

        public void SavePackageFile(Package package, Stream stream)
        {
            packageFileSvc.SavePackageFile(package, stream);
        }

        public void DeletePackage(string id, string version) {
            var package = FindPackageByIdAndVersion(id, version);

            if (package == null)
                throw new EntityException(Strings.PackageWithIdAndVersionNotFound, id, version);

            using (var tx = new TransactionScope()) {
                var packageRegistration = package.PackageRegistration;
                packageRepo.DeleteOnCommit(package);
                packageFileSvc.DeletePackageFile(id, version);
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

        public virtual Package FindPackageByIdAndVersion(string id, string version = null) {
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
                    .Where(p => p.PackageRegistration.Id == id).ToList();

            Package package = null;
            if (version == null) {
                package = packageVersions
                    .Where(p => p.IsLatest)
                    .SingleOrDefault();

                if (package == null && packageVersions.Any()) {
                    // Should never happen.
                    throw new InvalidOperationException("Packages are in a bad state. At least one should have IsLatest set");
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

        public IQueryable<Package> GetLatestVersionOfPublishedPackages() {
            return packageRepo.GetAll()
                .Include(x => x.PackageRegistration)
                .Include(x => x.Authors)
                .Include(x => x.PackageRegistration.Owners)
                .Where(package => package.Published != null && package.IsLatest && !package.Unlisted);
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
            var packageVersion = Version.Parse(package.Version);
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
            // TODO: improve setting the latest bit; this is horrible. Trigger maybe?
            foreach (var pv in packageRegistration.Packages)
                pv.IsLatest = false;

            var latestVersion = packageRegistration.Packages.Where(p => p.Published != null).
                Max(p => new Version(p.Version));

            packageRegistration.Packages.Where(pv => pv.Version == latestVersion.ToString()).Single().IsLatest = true;
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
            package.Unlisted = false;
            packageRepo.CommitChanges();
        }

        public void MarkPackageUnlisted(Package package) {
            package.Unlisted = true;
            packageRepo.CommitChanges();
        }
    }
}