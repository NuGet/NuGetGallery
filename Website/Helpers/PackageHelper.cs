using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NuGet;

namespace NuGetGallery.Helpers
{
    internal class PackageHelper
    {
        /// <summary>
        /// Look for the IPackage instance in the cache first. If it's in the cache, return it.
        /// Otherwise, download the package from the storage service and store it into the cache.
        /// </summary>
        public static async Task<IPackage> GetPackageFromCacheOrDownloadIt(
            Package package,
            IPackageCacheService cacheService,
            IPackageFileService packageFileService)
        {
            Debug.Assert(package != null);
            Debug.Assert(cacheService != null);
            Debug.Assert(packageFileService != null);

            string cacheKey = CreateCacheKey(package.PackageRegistration.Id, package.Version);
            byte[] buffer = cacheService.GetBytes(cacheKey);
            if (buffer == null)
            {
                // In the past, some very old packages can specify an external package binary not hosted at nuget.org.
                // We no longer allow that today.
                if (!String.IsNullOrEmpty(package.ExternalPackageUrl))
                {
                    throw new InvalidOperationException("The requested package is not hosted on blob storage.");
                }
                else
                {
                    using (Stream stream = await packageFileService.DownloadPackageFileAsync(package))
                    {
                        if (stream == null)
                        {
                            return null;
                        }

                        buffer = stream.ReadAllBytes();
                    }
                }

                cacheService.SetBytes(cacheKey, buffer);
            }

            return new ZipPackage(new MemoryStream(buffer));
        }

        private static string CreateCacheKey(string id, string version)
        {
            string key = id.ToLowerInvariant() + "." + version.ToLowerInvariant();

            byte[] bytes = Encoding.Unicode.GetBytes(key);

            using (var sha1 = SHA256.Create())
            {
                byte[] hash = sha1.ComputeHash(bytes);
                string encodedHash = Convert.ToBase64String(hash);

                // some Base64 characters are invalid for a file name.
                return encodedHash.Replace('+', '_').Replace('/', '-').Replace("=", "");
            }
        }
    }
}