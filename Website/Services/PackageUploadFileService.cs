using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NuGet;
using System.IO;

namespace NuGetGallery
{
    public class PackageUploadFileService : IPackageUploadFileService
    {
        readonly IFileStorageService fileStorageService;
        
        public PackageUploadFileService(IFileStorageService fileStorageService)
        {
            this.fileStorageService = fileStorageService;
        }
        
        public void DeleteUploadedFile(User user)
        {
            throw new Exception();
        }

        public NuGet.ZipPackage GetUploadedFile(User user)
        {
            throw new Exception();
        }

        public void SaveUploadedFile(
            int userKey,
            string packageId,
            string packageVersion,
            Stream packageFileStream)
        {
            if (userKey < 1)
                throw new ArgumentException("A user key is required.", "userKey");
            if (string.IsNullOrWhiteSpace(packageId))
                throw new ArgumentException("A package identifier is required.", "packageId");
            if (string.IsNullOrWhiteSpace(packageVersion))
                throw new ArgumentException("A package version is required.", "packageVersion");
            if (packageFileStream == null)
                throw new ArgumentNullException("packageFileStream");

            var packageUploadFileName = string.Format(Const.PackageUploadFileNameTemplate, userKey, Const.PackageFileExtension);

            fileStorageService.SaveFile(Const.PackageUploadsFolderName, packageUploadFileName, packageFileStream);
        }
    }
}