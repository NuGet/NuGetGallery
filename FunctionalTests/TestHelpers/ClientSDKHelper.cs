using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGet;
using System.Globalization;
using System.IO;

namespace NugetClientSDKHelpers
{
    /// <summary>
    /// Provides helper functions to query Gallery with Nuget.Core APIS.
    /// </summary>
    public class ClientSDKHelper
    {
        /// <summary>
        /// Returns the latest stable version string for the given package.
        /// </summary>
        /// <param name="packageId"></param>
        /// <returns></returns>
        public static string GetLatestStableVersion(string packageId)
        {
            IPackageRepository repo = PackageRepositoryFactory.Default.CreateRepository(sourceUrl) as IPackageRepository;
            IPackage package = repo.FindPackage(packageId);
            return package.Version.ToString();
        }

        /// <summary>
        /// Returns the download count of the given package as a formatted string as it would appear in the gallery UI.
        /// </summary>
        /// <param name="packageId"></param>
        /// <returns></returns>
        public static string GetFormattedDownLoadStatistics(string packageId)
        {
            IPackageRepository repo = PackageRepositoryFactory.Default.CreateRepository(sourceUrl) as IPackageRepository;        
            IPackage package =   repo.FindPackage(packageId);
            string formattedCount = package.DownloadCount.ToString("N1", CultureInfo.InvariantCulture);
            if (formattedCount.EndsWith(".0"))
                formattedCount = formattedCount.Remove(formattedCount.Length - 2);
            return formattedCount;
          
        }

        /// <summary>
        /// Searchs the gallery to get the packages matching the specific search term and returns their count.
        /// </summary>
        /// <param name="searchQuery"></param>
        /// <returns></returns>
        public static int GetPackageCountForSearchTerm(string searchQuery)
        {
            List<IPackage> packages;
            IPackageRepository repo = PackageRepositoryFactory.Default.CreateRepository(sourceUrl) as IPackageRepository;
            IServiceBasedRepository serviceRepo = repo as IServiceBasedRepository;
         
            if(serviceRepo != null)
            {
                packages =    serviceRepo.Search(searchQuery,false).ToList();
            }
            else
            {
                packages = repo.GetPackages().Find(searchQuery).FilterByPrerelease(false).ToList();
            }
            return packages.Count;
        }

        /// <summary>
        /// Given the path to the nupkg file, returns the corresponding package ID.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static string GetPackageIdFromNupkgFile(string filePath)
        {
            try
            {
                ZipPackage pack = new ZipPackage(filePath);
                return pack.Id;
            }
            catch (Exception e)
            {
                Console.WriteLine(" Exception thrown while trying to create zippackage for :{0}. Message {1}", filePath, e.Message);
                return null;
            }
            
        }

        /// <summary>
        /// Checks if the given package is present in the source.
        /// </summary>
        /// <param name="packageId"></param>
        /// <returns></returns>
        public static bool CheckIfPackageExistsInGallery(string packageId)
        {
            IPackageRepository repo = PackageRepositoryFactory.Default.CreateRepository(sourceUrl) as IPackageRepository;
            IPackage package = repo.FindPackage(packageId);
            return (package != null);
        }

        /// <summary>
        /// Clears the local machine cache.
        /// </summary>
        public static void ClearMachineCache()
        {
            NuGet.MachineCache.Default.Clear();
        }

        /// <summary>
        /// Clears the local package folder.
        /// </summary>
        public static void ClearLocalPackageFolder(string packageId)
        {
            string packageVersion = NugetClientSDKHelpers.ClientSDKHelper.GetLatestStableVersion(packageId);
            string expectedDownloadedNupkgFileName = packageId + "." + packageVersion;
            if(Directory.Exists(Path.Combine(Environment.CurrentDirectory, expectedDownloadedNupkgFileName)))
                Directory.Delete(expectedDownloadedNupkgFileName, true);
        }

        /// <summary>
        /// Given a package checks if it is installed properly in the current dir.
        /// </summary>
        /// <param name="packageId"></param>
        /// <returns></returns>
        public static bool CheckIfPackageInstalled(string packageId)
        {
            string packageVersion = NugetClientSDKHelpers.ClientSDKHelper.GetLatestStableVersion(packageId);
            string expectedDownloadedNupkgFileName = packageId + "." + packageVersion;
            //check if the nupkg file exists on the expected path post install
            string expectedNupkgFilePath = Path.Combine(Environment.CurrentDirectory, expectedDownloadedNupkgFileName, expectedDownloadedNupkgFileName + ".nupkg");
            if((!File.Exists(expectedNupkgFilePath)))
            {
                Console.WriteLine( " Package file {0} not present after download", expectedDownloadedNupkgFileName);
                return false;
            }
            string downloadedPackageId = ClientSDKHelper.GetPackageIdFromNupkgFile(expectedNupkgFilePath);
            //Check that the downloaded Nupkg file is not corrupt and it indeed corresponds to the package which we were trying to download.
            if(!(downloadedPackageId.Equals(packageId)))
            {
                Console.WriteLine( "Unable to unzip the package downloaded via Nuget Core. Check log for details");
                return false;
            }
            return true;
        }


        #region PrivateMembers
        private static string sourceUrl
        {
            get
            {
                return Utilities.BaseUrl + "api/v2";
            }
        }
        #endregion PrivateMemebers
    }
}
