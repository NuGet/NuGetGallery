using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Transactions;
using NuGet;

namespace NuGetGallery {
    public class PackageService : IPackageService {
        readonly ICryptographyService cryptoSvc;
        readonly IEntityRepository<PackageRegistration> packageRegistrationRepo;
        readonly IEntityRepository<Package> packageRepo;
        readonly IPackageFileService packageFileSvc;

        public PackageService(
            ICryptographyService cryptoSvc,
            IEntityRepository<PackageRegistration> packageRegistrationRepo,
            IEntityRepository<Package> packageRepo,
            IPackageFileService packageFileSvc) {
            this.cryptoSvc = cryptoSvc;
            this.packageRegistrationRepo = packageRegistrationRepo;
            this.packageRepo = packageRepo;
            this.packageFileSvc = packageFileSvc;
        }

        public Package CreatePackage(
            ZipPackage zipPackage,
            User currentUser) {
            var packageRegistration = packageRegistrationRepo.GetAll()
                .Where(p => p.Id == zipPackage.Id)
                .SingleOrDefault();

            if (packageRegistration != null)
                throw new EntityException("The package identifier '{0}' is not available.", packageRegistration.Id);
            else {
                packageRegistration = new PackageRegistration {
                    Id = zipPackage.Id
                };

                packageRegistration.Owners.Add(currentUser);

                packageRegistrationRepo.InsertOnCommit(packageRegistration);
            }

            var package = packageRegistration.Packages
                .Where(pv => pv.Version == zipPackage.Version.ToString())
                .SingleOrDefault();

            if (package != null)
                throw new EntityException("A package with identifier '{0}' and version '{1}' already exists.", packageRegistration.Id, package.Version);
            else {
                // TODO: add flattened authors, and other properties
                // TODO: add package size
                var now = DateTime.UtcNow;
                package = new Package {
                    Description = zipPackage.Description,
                    RequiresLicenseAcceptance = zipPackage.RequireLicenseAcceptance,
                    Version = zipPackage.Version.ToString(),
                    HashAlgorithm = cryptoSvc.HashAlgorithmId,
                    Hash = cryptoSvc.GenerateHash(zipPackage.GetStream().ReadAllBytes()),
                    Created = now,
                    LastUpdated = now,
                };

                foreach (var author in zipPackage.Authors)
                    package.Authors.Add(new PackageAuthor { Name = author });
                package.FlattenedAuthors = package.Authors.Flatten();

                if (zipPackage.LicenseUrl != null)
                    package.LicenseUrl = zipPackage.LicenseUrl.ToString();
                if (zipPackage.ProjectUrl != null)
                    package.ProjectUrl = zipPackage.ProjectUrl.ToString();
                if (zipPackage.Summary != null)
                    package.Summary = zipPackage.Summary;
                if (zipPackage.Tags != null)
                    package.Tags = zipPackage.Tags;
                if (zipPackage.Title != null)
                    package.Title = zipPackage.Title;

                packageRegistration.Packages.Add(package);
            }

            using (var tx = new TransactionScope())
            using (var stream = zipPackage.GetStream()) {
                packageFileSvc.Insert(
                    packageRegistration.Id,
                    package.Version,
                    stream);

                packageRegistrationRepo.CommitChanges();

                tx.Complete();
            }

            return package;
        }

        public Package FindById(string id) {
            return packageRepo.GetAll()
                .Include(pv => pv.PackageRegistration)
                .Where(pv => pv.PackageRegistration.Id == id)
                .SingleOrDefault();
        }

        public Package FindByIdAndVersion(
            string id,
            string version) {
            return packageRepo.GetAll()
                .Include(pv => pv.PackageRegistration)
                .Where(pv => pv.PackageRegistration.Id == id && pv.Version == version)
                .SingleOrDefault();
        }

        public IEnumerable<Package> GetLatestVersionOfPublishedPackages() {
            return packageRepo.GetAll()
                .Include(x => x.PackageRegistration)
                .Where(pv => pv.Published != null && pv.IsLatest)
                .ToList();
        }


        public void PublishPackage(Package package) {
            package.Published = DateTime.UtcNow;

            // TODO: improve setting the latest bit; this is horrible. Trigger maybe?
            foreach (var pv in package.PackageRegistration.Packages)
                pv.IsLatest = false;

            var latestVersion = package.PackageRegistration.Packages.Max(pv => new Version(pv.Version));

            package.PackageRegistration.Packages.Where(pv => pv.Version == latestVersion.ToString()).Single().IsLatest = true;

            packageRepo.CommitChanges();
        }
    }
}