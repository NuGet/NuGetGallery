using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using NuGet;

using HttpClient = System.Net.Http.HttpClient;

namespace NuGetGallery.Helpers
{
    internal class PackageHelper
    {
        /// <summary>
        /// Look for the INupkg instance in the cache first. If it's in the cache, return it.
        /// Otherwise, download the package from the storage service and store it into the cache.
        /// </summary>
        public static async Task<INupkg> GetPackageFromCacheOrDownloadIt(
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
                    var httpClient = new HttpClient();
                    using (var responseStream = await httpClient.GetStreamAsync(package.ExternalPackageUrl))
                    {
                        buffer = responseStream.ReadAllBytes();
                    }
                }
                else
                {
                    using (Stream stream = await packageFileService.DownloadPackageFileAsync(package))
                    {
                        if (stream == null)
                        {
                            throw new InvalidOperationException("Couldn't download the package from the storage.");
                        }

                        buffer = stream.ReadAllBytes();
                    }
                }

                cacheService.SetBytes(cacheKey, buffer);
            }

            return new Nupkg(new MemoryStream(buffer), leaveOpen: false);
        }

        private static string CreateCacheKey(string id, string version)
        {
            return id.ToLowerInvariant() + "." + version.ToLowerInvariant();
        }
    }
}