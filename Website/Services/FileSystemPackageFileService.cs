using System;
using System.IO;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class FileSystemPackageFileService : IPackageFileService
    {
        readonly IConfiguration configuration;
        readonly IFileSystemService fileSystemSvc;

        public FileSystemPackageFileService(
            IConfiguration configuration,
            IFileSystemService fileSystemSvc)
        {
            this.configuration = configuration;
            this.fileSystemSvc = fileSystemSvc;
        }

        public void SavePackageFile(Package package, Stream packageFile)
        {
            if (package == null)
                throw new ArgumentNullException("package");
            if (packageFile == null)
                throw new ArgumentNullException("packageFile");
            if (package.PackageRegistration == null || string.IsNullOrWhiteSpace(package.PackageRegistration.Id) || string.IsNullOrWhiteSpace(package.Version))
                throw new ArgumentException("The package is missing required data.", "package");

            if (!fileSystemSvc.DirectoryExists(configuration.PackageFileDirectory))
                fileSystemSvc.CreateDirectory(configuration.PackageFileDirectory);
            var path = BuildPackageFileSavePath(package.PackageRegistration.Id, package.Version);
            using (var file = fileSystemSvc.OpenWrite(path))
            {
                packageFile.CopyTo(file);
            }
        }

        public ActionResult CreateDownloadPackageResult(Package package)
        {
            if (package == null)
                throw new ArgumentNullException("package");
            if (package.PackageRegistration == null || string.IsNullOrWhiteSpace(package.PackageRegistration.Id) || string.IsNullOrWhiteSpace(package.Version))
                throw new ArgumentException("The package is missing required data.", "package");

            var fileName = BuildPackageFileSavePath(package.PackageRegistration.Id, package.Version);
            var result = new FilePathResult(fileName, Const.PackageContentType);
            result.FileDownloadName = new FileInfo(fileName).Name;
            return result;
        }

        string BuildPackageFileDownloadFileName(string id, string version)
        {
            return Path.Combine(
                configuration.PackageFileDirectory,
                string.Format(Const.PackageFileSavePathTemplate, id, version));
        }

        string BuildPackageFileSavePath(string id, string version)
        {
            return Path.Combine(
                configuration.PackageFileDirectory,
                string.Format(Const.PackageFileSavePathTemplate, id, version, Const.PackageFileExtension));
        }

        public void DeletePackageFile(string id, string version)
        {
            var path = BuildPackageFileSavePath(id, version);
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}