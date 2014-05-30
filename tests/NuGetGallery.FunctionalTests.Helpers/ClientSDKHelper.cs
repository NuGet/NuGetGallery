using NuGet;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace NuGetGallery.FunctionTests.Helpers
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
            List<IPackage> packages = repo.FindPackagesById(packageId).ToList();
            packages = packages.Where(item => item.IsListed()).ToList();
            packages = packages.Where(item => item.IsReleaseVersion()).ToList();
            SemanticVersion version = packages.Max(item => item.Version);
            return version.ToString();
        }

        /// <summary>
        /// Returns the count of versions available for the given package
        /// </summary>
        /// <param name="packageId"></param>
        /// <returns></returns>
        public static int GetVersionCount(string packageId,bool allowPreRelease=true)
        {
            IPackageRepository repo = PackageRepositoryFactory.Default.CreateRepository(sourceUrl) as IPackageRepository;
            List<IPackage> packages = repo.FindPackagesById(packageId).ToList();
            if (packages != null)
            {
                if(!allowPreRelease)
                    packages = packages.Where(item => item.IsReleaseVersion()).ToList();
                return packages.Count;
            }
            else
                return 0;                
        }
        /// <summary>
        /// Returns the download count of the given package as a formatted string as it would appear in the gallery UI.
        /// </summary>
        /// <param name="packageId"></param>
        /// <returns></returns>
        public static string GetFormattedDownLoadStatistics(string packageId)
        {           
            string formattedCount = GetDownLoadStatistics(packageId).ToString("N1", CultureInfo.InvariantCulture);
            if (formattedCount.EndsWith(".0"))
                formattedCount = formattedCount.Remove(formattedCount.Length - 2);
            return formattedCount;         
        }

        /// <summary>
        /// Returns the download count of the given package.
        /// </summary>
        /// <param name="packageId"></param>
        /// <returns></returns>
        public static int GetDownLoadStatistics(string packageId)
        {
            IPackageRepository repo = PackageRepositoryFactory.Default.CreateRepository(sourceUrl) as IPackageRepository;
            IPackage package = repo.FindPackage(packageId);
            return package.DownloadCount;
        }

        /// <summary>
        /// Returns the download count of the specific version of the package.
        /// </summary>
        /// <param name="packageId"></param>
        /// <returns></returns>
        public static int GetDownLoadStatisticsForPackageVersion(string packageId,string packageVersion)
        {
            IPackageRepository repo = PackageRepositoryFactory.Default.CreateRepository(sourceUrl) as IPackageRepository;
            IPackage package = repo.FindPackage(packageId, new SemanticVersion(packageVersion));
            return package.DownloadCount;
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
            IPackageRepository serviceRepo = repo as IPackageRepository;
         
            if(serviceRepo != null)
            {
                packages =    serviceRepo.Search(searchQuery,false).ToList();
            }
            else
            {
                packages = repo.GetPackages().Find(searchQuery).ToList();
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
        /// Given the path to the nupkg file, returns the corresponding package ID.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static bool IsPackageVersionUnListed(string packageId,string version)
        {
            IPackageRepository repo = PackageRepositoryFactory.Default.CreateRepository(sourceUrl) as IPackageRepository;
            IPackage package = repo.FindPackage(packageId, new SemanticVersion(version), true, true);
            if (package != null)
                return !package.Listed;
            else
                return false;  
        }

        /// <summary>
        /// Checks if the given package is present in the source.
        /// </summary>
        /// <param name="packageId"></param>
        /// <returns></returns>
        public static bool CheckIfPackageExistsInSource(string packageId, string sourceUrl)
        {
            IPackageRepository repo = PackageRepositoryFactory.Default.CreateRepository(sourceUrl) as IPackageRepository;
            IPackage package = repo.FindPackage(packageId);
            return (package != null);
        }

        /// <summary>
        /// Checks if the given package is present in the source.
        /// </summary>
        /// <param name="packageId"></param>
        /// <returns></returns>
        public static bool CheckIfPackageVersionExistsInSource(string packageId, string version, string sourceUrl)
        {
            bool found = false;
            // A new way to check if packages exist in source. The logic above often gives timeout errors randomly. 
            IPackageRepository repo = PackageRepositoryFactory.Default.CreateRepository(sourceUrl) as IPackageRepository;
            SemanticVersion semVersion;
            bool success = SemanticVersion.TryParse(version, out semVersion);
            if (success)
            {
                try
                {
                    for (int i = 0; ((i < 5) && (!found)); i++)
                    {
                        // Wait for the search service to kick in, so that the package can be found via FindPackage(packageId, SemanticVersion)
                        Thread.Sleep(60 * 1000);
                        IPackage package = repo.FindPackage(packageId, semVersion);
                        found = (package != null);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception thrown while checking the existence of package {0} with version (1}:\r\n {2}", packageId, version, ex.Message);
                }
            }

            if (found)
            {
                Console.WriteLine("Found package {0} with version {1} in sourceUrl of {2}", packageId, version, sourceUrl);
            }
            return found;
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
            string packageVersion = ClientSDKHelper.GetLatestStableVersion(packageId);
            string expectedDownloadedNupkgFileName = packageId + "." + packageVersion;
            string pathToNupkgFolder = Path.Combine(Environment.CurrentDirectory, expectedDownloadedNupkgFileName);
            Console.WriteLine("Path to the downloaded Nupkg file for clearing local package folder is: " + pathToNupkgFolder);
            if(Directory.Exists(pathToNupkgFolder))
                Directory.Delete(expectedDownloadedNupkgFileName, true);
        }

        /// <summary>
        /// Given a package checks if it is installed properly in the current dir.
        /// </summary>
        /// <param name="packageId"></param>
        /// <returns></returns>
        public static bool CheckIfPackageInstalled(string packageId)
        {
            string packageVersion = ClientSDKHelper.GetLatestStableVersion(packageId);
            return CheckIfPackageVersionInstalled(packageId, packageVersion);
        }


        /// <summary>
        /// Given a package checks if it that version of the package is installed.
        /// </summary>
        /// <param name="packageId"></param>
        /// <returns></returns>
        public static bool CheckIfPackageVersionInstalled(string packageId, string packageVersion)
        {
            //string packageVersion = ClientSDKHelper.GetLatestStableVersion(packageId);
            string expectedDownloadedNupkgFileName = packageId + "." + packageVersion;
            //check if the nupkg file exists on the expected path post install
            string expectedNupkgFilePath = Path.Combine(Environment.CurrentDirectory, expectedDownloadedNupkgFileName, expectedDownloadedNupkgFileName + ".nupkg");
            Console.WriteLine("The expected Nupkg file path after package install is: " + expectedNupkgFilePath);
            if ((!File.Exists(expectedNupkgFilePath)))
            {
                Console.WriteLine(" Package file {0} not present after download", expectedDownloadedNupkgFileName);
                return false;
            }
            string downloadedPackageId = ClientSDKHelper.GetPackageIdFromNupkgFile(expectedNupkgFilePath);
            //Check that the downloaded Nupkg file is not corrupt and it indeed corresponds to the package which we were trying to download.
            if (!(downloadedPackageId.Equals(packageId)))
            {
                Console.WriteLine("Unable to unzip the package downloaded via Nuget Core. Check log for details");
                return false;
            }
            return true;
        }


        #region PrivateMembers
        private static string sourceUrl
        {
            get
            {
                return UrlHelper.BaseUrl + "api/v2";
            }
        }
        #endregion PrivateMemebers
    }
}
