using System;
using System.IO;
using System.Web.Mvc;

namespace NuGetGallery {
    public class FileSystemPackageFileService : IPackageFileService {
        // TODO: abstract file system so we can write unit tests

        readonly IConfiguration configuration;
        readonly IEntityRepository<Package> packageRepo;
        readonly IFileSystemService fileSystemSvc;

        public FileSystemPackageFileService(
            IConfiguration configuration,
            IEntityRepository<Package> packageRepo,
            IFileSystemService fileSystemSvc) {
            this.configuration = configuration;
            this.packageRepo = packageRepo;
            this.fileSystemSvc = fileSystemSvc;
        }
        
        public void SavePackageFile(
            string packageId, 
            string packageVersion, 
            Stream packageFile) {
            // TODO: verify that the package and version actually exist?

            if (!fileSystemSvc.DirectoryExists(configuration.PackageFileDirectory))
                fileSystemSvc.CreateDirectory(configuration.PackageFileDirectory);

            var path = Path.Combine(
                configuration.PackageFileDirectory,
                string.Format(Const.SavePackageFilePathTemplate, packageId, packageVersion, Const.PackageExtension));

            using (var file = fileSystemSvc.OpenWrite(path)) {
                packageFile.CopyTo(file);
            }
        }

        public ActionResult CreateDownloadPackageResult(
            string packageId,
            string packageVersion) {
            throw new NotImplementedException();
        }


        public Uri GetDownloadUri(
            string id,
            string version) {
            // TODO: validate inputs

            return new Uri(
                new Uri(configuration.BaseUrl, UriKind.Absolute),
                new Uri(string.Format("Packages/{0}.{1}.{2}", id, version, Const.PackageExtension), UriKind.Relative));
        }
    }
}