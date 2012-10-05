using System;
using System.Diagnostics;
using System.IO;
using NuGet;

namespace NuGetGallery.Helpers
{
	internal class PackageHelper
	{
		/// <summary>
		/// Look for the IPackage instance in the cache first. If it's in the cache, return it.
		/// Otherwise, download the package from the storage service and store it into the cache.
		/// </summary>
		public static IPackage GetPackageFromCacheOrDownloadIt(
			Package package,			
			ICacheService cacheService, 
			IPackageFileService packageFileService)
		{
			Debug.Assert(package != null);
			Debug.Assert(cacheService != null);
			Debug.Assert(packageFileService != null);

			string cacheKey = CreateCacheKey(package.PackageRegistration.Id, package.Version);
			var item = cacheService.GetItem(cacheKey) as IPackage;
			if (item == null)
			{
				using (Stream stream = packageFileService.DownloadPackageFile(package))
				{
					if (stream == null)
					{
						throw new InvalidOperationException("Couldn't download the package from the storage.");
					}

					item = new ZipPackage(stream);
					cacheService.SetItem(cacheKey, item, TimeSpan.FromMinutes(5));
				}
			}

			return item;
		}

		private static string CreateCacheKey(string id, string version)
		{
			return id.ToLowerInvariant() + "." + version.ToLowerInvariant();
		}
	}
}