using System;
using System.IO;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class FileSystemPackageFileService : IPackageFileService
    {
        // TODO: abstract file system so we can write unit tests

        readonly IConfiguration configuration;
        readonly IEntityRepository<Package> packageRepo;
        
        public FileSystemPackageFileService(
            IConfiguration configuration,
            IEntityRepository<Package> packageRepo)
        {
            this.configuration = configuration;
            this.packageRepo = packageRepo;
        }
        
        public void Insert(
            string packageId, 
            string packageVersion, 
            Stream packageFile)
        {
            // TODO: verify that the package and version actually exist?

            if (!Directory.Exists(configuration.PackageFileDirectory))
                Directory.CreateDirectory(configuration.PackageFileDirectory);
            
            var path = Path.Combine(
                configuration.PackageFileDirectory, 
                string.Format("{0}.{1}{2}", packageId, packageVersion, Const.PackageExtension));

            using (var file = File.OpenWrite(path))
            {
                packageFile.CopyTo(file);
            }
        }

        public ActionResult CreateDownloadPackageResult(
            string packageId, 
            string packageVersion)
        {
            throw new NotImplementedException();
        }


        public Uri GetDownloadUri(
            string id, 
            string version)
        {
            // TODO: validate inputs

            return new Uri(
                new Uri(configuration.BaseUrl, UriKind.Absolute),
                new Uri(string.Format("Packages/{0}.{1}.{2}", id, version, Const.PackageExtension), UriKind.Relative));
        }
    }
}