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
            IPackage nugetPackage,
            User currentUser) {
            var packageRegistration = CreateOrGetPackageRegistration(currentUser, nugetPackage);

            var package = CreatePackageFromNuGetPackage(packageRegistration, nugetPackage);
            packageRegistration.Packages.Add(package);

            using (var tx = new TransactionScope())
            using (var stream = nugetPackage.GetStream()) {
                packageFileSvc.SavePackageFile(
                    packageRegistration.Id,
                    package.Version,
                    stream);

                packageRegistrationRepo.CommitChanges();

                tx.Complete();
            }

            return package;
        }

        public virtual PackageRegistration FindPackageRegistrationById(string id) {
            return packageRegistrationRepo.GetAll()
                .Where(pr => pr.Id == id)
                .SingleOrDefault();
        }

        public Package FindPackageByIdAndVersion(
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

        PackageRegistration CreateOrGetPackageRegistration(
            User currentUser,
            IPackage nugetPackage)
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

        Package CreatePackageFromNuGetPackage(
            PackageRegistration packageRegistration,
            IPackage nugetPackage)
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
                package.Dependencies.Add(new PackageDependency { Id = dependency.Id, VersionRange = dependency.VersionSpec.ToString() });

            package.FlattenedAuthors = package.Authors.Flatten();
            package.FlattenedDependencies = package.Dependencies.Flatten();

            return package;
        }
    }
}