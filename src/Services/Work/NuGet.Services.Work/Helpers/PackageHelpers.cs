using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Work.Jobs.Models;

namespace NuGet.Services.Work
{
    public static class PackageHelpers
    {
        private const string PackageBlobNameFormat = "{0}.{1}.nupkg";
        private const string PackageBackupBlobNameFormat = "packages/{0}/{1}/{2}.nupkg";

        public static string GetPackageBlobName(PackageRef package)
        {
            return GetPackageBlobName(package.Id, package.Version);
        }

        public static string GetPackageBlobName(string id, string version)
        {
            return String.Format(
                CultureInfo.InvariantCulture, 
                PackageBlobNameFormat, 
                id, 
                version).ToLowerInvariant();
        }

        public static string GetPackageBackupBlobName(PackageRef package)
        {
            return GetPackageBackupBlobName(package.Id, package.Version, package.Hash);
        }

        public static string GetPackageBackupBlobName(string id, string version, string hash)
        {
            return String.Format(
                CultureInfo.InvariantCulture, 
                PackageBackupBlobNameFormat, 
                id, 
                version, 
                WebUtility.UrlEncode(hash)).ToLowerInvariant();
        }
    }
}
