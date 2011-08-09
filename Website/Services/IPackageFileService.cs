using System;
using System.IO;
using System.Web.Mvc;

namespace NuGetGallery
{
    public interface IPackageFileService
    {
        void Insert(
            string packageId,
            string packageVersion, 
            Stream packageFile);

        Uri GetDownloadUri(
            string packageId, 
            string packageVersion);

        // TODO: This really doesn't belong here.
        ActionResult CreateDownloadPackageResult(
            string packageId,
            string packageVersion);
    }
}