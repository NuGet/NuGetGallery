// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGetGallery.FunctionalTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests
{
    public class ClientSdkHelper
        : HelperBase
    {
        private static readonly TimeSpan SleepDuration = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan TotalSleepDuration = TimeSpan.FromMinutes(30);
        private static readonly int Attempts = (int) (TotalSleepDuration.Ticks / SleepDuration.Ticks);

        private static readonly object ExistingPackagesLock = new object();
        private static readonly IList<PackageInfo> Packages = new List<PackageInfo>();

        public ClientSdkHelper(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        private static string SourceUrl
        {
            get
            {
                return UrlHelper.BaseUrl + "api/v2";
            }
        }

        private static async Task<IEnumerable<IPackageSearchMetadata>> GetPackageMetadataAsync(
            string packageId,
            bool includePrerelease = true,
            bool includeUnlisted = true,
            string sourceUrl = null)
        {
            var resource = Repository.Factory.GetCoreV2(new PackageSource(sourceUrl ?? SourceUrl))
                .GetResource<PackageMetadataResource>();
            using var cacheContext = new SourceCacheContext { NoCache = true };
            return await resource.GetMetadataAsync(
                packageId, includePrerelease, includeUnlisted,
                cacheContext, NullLogger.Instance, CancellationToken.None);
        }

        /// <summary>
        /// Checks if the given package is present in the source.
        /// </summary>
        public bool CheckIfPackageExistsInSource(string packageId, string sourceUrl)
        {
            WriteLine("Checking if package {0} exists in source {1}... ", packageId, sourceUrl);
            var packageExistsInSource = GetPackageMetadataAsync(packageId, sourceUrl: sourceUrl).Result.Any();
            if (packageExistsInSource)
            {
                WriteLine("Found!");
            }
            else
            {
                WriteLine("NOT Found!");
            }

            return packageExistsInSource;
        }

        /// <summary>
        /// Checks if the given package version is present in V2. This method bypasses the hijack.
        /// </summary>
        private async Task<bool> CheckIfPackageVersionExistsInV2Async(string packageId, string version, bool? shouldBeListed)
        {
            var sourceUrl = UrlHelper.V2FeedRootUrl;
            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();

            var url = UrlHelper.V2FeedRootUrl + $"/Packages(Id='{packageId}',Version='{normalizedVersion}')?hijack=false";
            using (var httpClient = new System.Net.Http.HttpClient())
            {
                return await VerifyWithRetryAsync(
                    $"Verifying that package {packageId} {version} exists on source {sourceUrl} (non-hijacked).",
                    async () =>
                    {
                        using (var response = await httpClient.GetAsync(url))
                        {
                            if (response.StatusCode == HttpStatusCode.NotFound)
                            {
                                return false;
                            }
                            else if (response.StatusCode == HttpStatusCode.OK)
                            {
                                if (shouldBeListed.HasValue)
                                {
                                    var responseString = await response.Content.ReadAsStringAsync();
                                    var isActuallyListed = !responseString.Contains("<d:Published m:type=\"Edm.DateTime\">1900-01-01T00:00:00</d:Published>");
                                    return shouldBeListed == isActuallyListed;
                                }

                                return true;
                            }

                            response.EnsureSuccessStatusCode();
                            return false;
                        }
                    });
            }
        }

        /// <summary>
        /// Checks if the given package version is present in V2 and V3. This method depends on V2 hijacking to V3.
        /// </summary>
        private async Task<bool> CheckIfPackageVersionExistsInV2AndV3Async(string packageId, string version)
        {
            var sourceUrl = UrlHelper.V2FeedRootUrl;
            var nugetVersion = NuGetVersion.Parse(version);

            return await VerifyWithRetryAsync(
                $"Verifying that package {packageId} {version} exists on source {sourceUrl} (hijacked).",
                async () =>
                {
                    var metadata = await GetPackageMetadataAsync(packageId, sourceUrl: sourceUrl);

                    return metadata.Any(m => m.Identity.Version == nugetVersion);
                });
        }

        private async Task<bool> VerifyWithRetryAsync(string actionPhrase, Func<Task<bool>> actionAsync)
        {
            bool success = false;

            WriteLine($"{actionPhrase} ({Attempts} attempts, interval {SleepDuration.TotalSeconds} seconds).");

            for (var i = 0; i < Attempts && !success; i++)
            {
                if (i != 0)
                {
                    await Task.Delay(SleepDuration);
                }

                WriteLine($"[verification attempt {i}]: Executing... ");

                try
                {
                    success = await actionAsync();
                }
                catch (Exception ex)
                {
                    WriteLine($"[verification attempt {i}] threw an exception.{Environment.NewLine}{ex}");
                }

                if (success)
                {
                    WriteLine("Successful!");
                }
            }

            return success;
        }

        public async Task<PackageInfo> UploadPackage(string apiKey = null)
        {
            PackageInfo packageInfo = null;

            lock (ExistingPackagesLock)
            {
                packageInfo = Packages.FirstOrDefault(p => p.WasUploadedByApiKey(apiKey));
            }

            if (packageInfo == null)
            {
                packageInfo = await PackageInfo.CreateForUpload(this, apiKey);
            }

            return packageInfo;
        }

        public Task FailToUploadPackage(string apiKey = null)
        {
            return UploadPackage(UploadHelper.GetUniquePackageId(), UploadHelper.GetUniquePackageVersion(), apiKey, success: false);
        }

        public async Task<PackageInfo> UploadPackageVersion(string apiKey = null)
        {
            PackageInfo packageInfo = null;

            lock (ExistingPackagesLock)
            {
                var pushablePackageInfos = Packages.Where(p => p.CanUseApiKeyToPushNewVersion(apiKey));

                packageInfo = pushablePackageInfos
                    .GroupBy(p => p.Id)
                    .Where(pr => pr.Count() > 1)
                    .Select(pr => pr.Skip(1))
                    .SelectMany(pr => pr)
                    .LastOrDefault(p => p.WasUploadedByApiKey(apiKey));

                if (packageInfo != null)
                {
                    return packageInfo;
                }

                packageInfo = pushablePackageInfos.LastOrDefault();
            }

            if (packageInfo == null)
            {
                packageInfo = await PackageInfo.CreateForUpload(this, GetApiKeyWithSameOwnerThatCanUpload(apiKey));
            }

            return await packageInfo.PushNewVersion(this, apiKey);
        }

        /// <summary>
        /// Gets an API key with the same owner as <paramref name="apiKey"/>.
        /// </summary>
        /// <remarks>
        /// This function is used to create a package that <paramref name="apiKey"/> can be used on if no existing package can be used.
        /// In other words, if a test requires unlisting a package with an API key, but there is no existing package that can be unlisted by that API key, this function is used to find an API key that can be used to upload a package for that API key to then unlist.
        /// </remarks>
        private static string GetApiKeyWithSameOwnerThatCanUpload(string apiKey = null)
        {
            if (apiKey == null ||
                apiKey == GalleryConfiguration.Instance.Account.ApiKey ||
                apiKey == GalleryConfiguration.Instance.Account.ApiKeyPush ||
                apiKey == GalleryConfiguration.Instance.Account.ApiKeyPushVersion ||
                apiKey == GalleryConfiguration.Instance.Account.ApiKeyUnlist)
            {
                return GalleryConfiguration.Instance.Account.ApiKey;
            }
            else if (apiKey == GalleryConfiguration.Instance.AdminOrganization.ApiKey)
            {
                return GalleryConfiguration.Instance.AdminOrganization.ApiKey;
            }
            else if (apiKey == GalleryConfiguration.Instance.CollaboratorOrganization.ApiKey)
            {
                return GalleryConfiguration.Instance.CollaboratorOrganization.ApiKey;
            }

            throw new ArgumentOutOfRangeException(nameof(apiKey));
        }

        public async Task FailToUploadPackageVersion(string apiKey = null)
        {
            PackageInfo packageInfo = null;

            lock (ExistingPackagesLock)
            {
                packageInfo = Packages.LastOrDefault(p => p.HasSameOwnerAsApiKey(apiKey));
            }

            if (packageInfo == null)
            {
                packageInfo = await PackageInfo.CreateForUpload(this, GetApiKeyWithSameOwnerThatCanUpload(apiKey));
            }

            await packageInfo.FailToUploadNewVersion(this, apiKey);
        }

        public async Task<PackageInfo> UnlistPackage(string apiKey = null)
        {
            PackageInfo packageInfo = null;

            lock (ExistingPackagesLock)
            {
                packageInfo = Packages.LastOrDefault(p => p.WasUnlistedByApiKey(apiKey));

                if (packageInfo != null)
                {
                    return packageInfo;
                }

                packageInfo = Packages.LastOrDefault(p => p.Listed && p.CanUseApiKeyToPushNewVersion(apiKey));
            }

            if (packageInfo == null)
            {
                packageInfo = await PackageInfo.CreateForUpload(this, GetApiKeyWithSameOwnerThatCanUpload(apiKey));
            }

            await packageInfo.Unlist(this, apiKey);
            return packageInfo;
        }

        public async Task FailToUnlistPackage(string apiKey = null)
        {
            PackageInfo packageInfo = null;

            lock (ExistingPackagesLock)
            {
                packageInfo = Packages.LastOrDefault(p => p.HasSameOwnerAsApiKey(apiKey));
            }

            if (packageInfo == null)
            {
                packageInfo = await PackageInfo.CreateForUpload(this, GetApiKeyWithSameOwnerThatCanUpload(apiKey));
            }

            await packageInfo.FailToUnlist(this, apiKey);
        }
        
        private async Task UploadPackage(string packageId, string version, string apiKey = null, bool success = true)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException($"{nameof(packageId)} cannot be null or empty!");
            }

            if (string.IsNullOrEmpty(version))
            {
                throw new ArgumentException($"{nameof(version)} cannot be null or empty!");
            }

            await Task.Yield();

            WriteLine("Uploading new package '{0}', version '{1}'", packageId, version);

            var packageCreationHelper = new PackageCreationHelper(TestOutputHelper);
            var packageFullPath = await packageCreationHelper.CreatePackage(packageId, version);

            try
            {
                var commandlineHelper = new CommandlineHelper(TestOutputHelper);
                var processResult = await commandlineHelper.UploadPackageAsync(packageFullPath, UrlHelper.V2FeedPushSourceUrl, apiKey);

                if (success)
                {
                    Assert.True(processResult.ExitCode == 0,
                        "The package upload via Nuget.exe did not succeed properly. Check the logs to see the process error and output stream.  Exit Code: " +
                        processResult.ExitCode + ". Error message: \"" + processResult.StandardError + "\"");

                    await VerifyPackageExistsInV2Async(packageId, version);
                }
                else
                {
                    Assert.False(processResult.ExitCode == 0,
                        "The package upload via Nuget.exe succeeded but was expected to fail. Check the logs to see the process error and output stream.  Exit Code: " +
                        processResult.ExitCode + ". Error message: \"" + processResult.StandardError + "\"");
                }
            }
            finally
            {
                // Delete package from local disk once it gets uploaded
                CleanCreatedPackage(packageFullPath);
            }
        }

        private async Task UnlistPackage(string packageId, string version, string apiKey = null, bool success = true)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException($"{nameof(packageId)} cannot be null or empty!");
            }

            if (string.IsNullOrEmpty(version))
            {
                throw new ArgumentException($"{nameof(version)} cannot be null or empty!");
            }

            await Task.Yield();

            WriteLine("Unlisting package '{0}', version '{1}'", packageId, version);

            var commandlineHelper = new CommandlineHelper(TestOutputHelper);
            var processResult = await commandlineHelper.DeletePackageAsync(packageId, version, UrlHelper.V2FeedPushSourceUrl, apiKey);

            if (success)
            {
                Assert.True(processResult.ExitCode == 0,
                    "The package unlist via Nuget.exe did not succeed properly. Check the logs to see the process error and output stream.  Exit Code: " +
                    processResult.ExitCode + ". Error message: \"" + processResult.StandardError + "\"");

                await VerifyPackageExistsInV2Async(packageId, version);
            }
            else
            {
                Assert.False(processResult.ExitCode == 0,
                    "The package unlist via Nuget.exe succeeded but was expected to fail. Check the logs to see the process error and output stream.  Exit Code: " +
                    processResult.ExitCode + ". Error message: \"" + processResult.StandardError + "\"");
            }
        }

        public class PackageInfo
        {
            public string Id { get; }
            public string Version { get; }
            public bool Listed { get; private set; }
            private string UploadApiKey { get; }
            private string UnlistApiKey { get; set; }
            private Task PackageIsReady { get; set; }

            public PackageInfo(string id, string version, string uploadApiKey)
            {
                Id = id;
                Version = version;
                Listed = true;
                UploadApiKey = uploadApiKey;
            }

            public static Task<PackageInfo> CreateForUpload(ClientSdkHelper helper, string uploadApiKey = null)
            {
                return CreateForUpload(UploadHelper.GetUniquePackageId(), helper, uploadApiKey);
            }

            private static async Task<PackageInfo> CreateForUpload(string id, ClientSdkHelper helper, string uploadApiKey = null)
            {
                var packageInfo = new PackageInfo(
                    id,
                    UploadHelper.GetUniquePackageVersion(),
                    uploadApiKey);

                packageInfo.PackageIsReady = helper.UploadPackage(packageInfo.Id, packageInfo.Version, uploadApiKey, success: true);

                lock (ExistingPackagesLock)
                {
                    Packages.Add(packageInfo);
                }

                await packageInfo.PackageIsReady;
                return packageInfo;
            }

            public async Task<PackageInfo> PushNewVersion(ClientSdkHelper helper, string uploadApiKey = null)
            {
                if (!CanUseApiKeyToPushNewVersion(uploadApiKey))
                {
                    throw new ArgumentException($"Cannot use {uploadApiKey} to push a new version of a package ({Id} {Version}) that was pushed by {UploadApiKey}.", nameof(uploadApiKey));
                }

                await PackageIsReady;
                return await CreateForUpload(Id, helper, uploadApiKey);
            }

            public async Task FailToUploadNewVersion(ClientSdkHelper helper, string uploadApiKey = null)
            {
                if (CanUseApiKeyToPushNewVersion(uploadApiKey))
                {
                    throw new ArgumentException($"Cannot use {uploadApiKey} to fail to push a new version of a package ({Id} {Version}) that was pushed by {UploadApiKey}.", nameof(uploadApiKey));
                }

                await PackageIsReady;
                await helper.UploadPackage(Id, UploadHelper.GetUniquePackageVersion(), uploadApiKey, success: false);
            }

            public async Task Unlist(ClientSdkHelper helper, string unlistApiKey = null)
            {
                if (!CanUseApiKeyToUnlist(unlistApiKey))
                {
                    throw new ArgumentException($"Cannot use {unlistApiKey} to unlist a package ({Id} {Version}) that was pushed by {UploadApiKey}.", nameof(unlistApiKey));
                }

                await PackageIsReady;

                lock (ExistingPackagesLock)
                {
                    Listed = false;
                    UnlistApiKey = unlistApiKey;
                    PackageIsReady = helper.UnlistPackage(Id, Version, unlistApiKey, success: true);
                }

                await PackageIsReady;
            }

            public async Task FailToUnlist(ClientSdkHelper helper, string unlistApiKey = null)
            {
                if (CanUseApiKeyToUnlist(unlistApiKey))
                {
                    throw new ArgumentException($"Cannot use {unlistApiKey} to fail to unlist a package ({Id} {Version}) that was pushed by {UploadApiKey}.", nameof(unlistApiKey));
                }

                await PackageIsReady;
                await helper.UnlistPackage(Id, UploadHelper.GetUniquePackageVersion(), unlistApiKey, success: false);
            }

            public bool HasSameOwnerAsApiKey(string apiKey)
            {
                return MapApiKeyToOwner(UploadApiKey) == MapApiKeyToOwner(apiKey);
            }

            public bool CanUseApiKeyToPushNewVersion(string apiKey)
            {
                // An API key can be used to upload a new version of this package if
                // - it has the same owner as the API key that was used to push
                // - it has a scope that allows pushing new versions
                return HasSameOwnerAsApiKey(apiKey) && 
                    apiKey != GalleryConfiguration.Instance.Account.ApiKeyUnlist;
            }
            
            public bool CanUseApiKeyToUnlist(string apiKey)
            {
                // An API key can be used to unlist this package if
                // - it has the same owner as the API key that was used to push
                // - it has a scope that allows unlisting
                return HasSameOwnerAsApiKey(apiKey) && 
                    apiKey != GalleryConfiguration.Instance.Account.ApiKeyPush && 
                    apiKey != GalleryConfiguration.Instance.Account.ApiKeyPushVersion;
            }

            public bool WasUploadedByApiKey(string apiKey)
            {
                return apiKey == UploadApiKey;
            }

            public bool WasUnlistedByApiKey(string apiKey)
            {
                return apiKey == UnlistApiKey;
            }

            private static string MapApiKeyToOwner(string apiKey)
            {
                if (apiKey == null ||
                    apiKey == GalleryConfiguration.Instance.Account.ApiKey ||
                    apiKey == GalleryConfiguration.Instance.Account.ApiKeyPush ||
                    apiKey == GalleryConfiguration.Instance.Account.ApiKeyPushVersion ||
                    apiKey == GalleryConfiguration.Instance.Account.ApiKeyUnlist)
                {
                    return GalleryConfiguration.Instance.Account.Name;
                }
                else if (apiKey == GalleryConfiguration.Instance.AdminOrganization.ApiKey)
                {
                    return GalleryConfiguration.Instance.AdminOrganization.Name;
                }
                else if (apiKey == GalleryConfiguration.Instance.CollaboratorOrganization.ApiKey)
                {
                    return GalleryConfiguration.Instance.CollaboratorOrganization.Name;
                }

                throw new ArgumentOutOfRangeException(nameof(apiKey));
            }
        }

        public async Task VerifyPackageExistsInV2AndV3Async(string packageId, string version)
        {
            var packageExistsInSource = await CheckIfPackageVersionExistsInV2AndV3Async(packageId, version);
            Assert.True(packageExistsInSource,
                $"Package {packageId} with version {version} is not found on the site {UrlHelper.V2FeedRootUrl}.");
        }

        public async Task VerifyPackageExistsInV2Async(string packageId, string version, bool? listed = null)
        {
            var packageExistsInSource = await CheckIfPackageVersionExistsInV2Async(packageId, version, listed);
            Assert.True(
                packageExistsInSource,
                $"Package {packageId} with version {version}{(listed.HasValue ? (listed.Value ? " listed" : " unlisted") : "")} is not found on the site {UrlHelper.V2FeedRootUrl}.");
        }

        /// <summary>
        /// Returns the latest stable version string for the given package.
        /// </summary>
        public static string GetLatestStableVersion(string packageId)
        {
            var packages = GetPackageMetadataAsync(packageId, includePrerelease: false, includeUnlisted: false).Result;
            var version = packages.Max(item => item.Identity.Version);
            return version.ToString();
        }

        public async Task VerifyVersionCountAsync(string packageId, int expectedVersionCount, bool allowPreRelease = true)
        {
            // To verify the count of package versions, the FindPackagesById() V2 OData endpoint is used. When the
            // gallery handles this request, it delegates to the search service. Since the search service can lag being
            // the gallery database (due to the time it takes for packages to make it through the V3 pipeline and into
            // an active Lucene index), we retry the request for a while.
            Assert.True(await VerifyWithRetryAsync(
                $"Verifying count of {packageId} versions is {expectedVersionCount}",
                async () =>
                {
                    var packages = (await GetPackageMetadataAsync(packageId, includePrerelease: allowPreRelease)).ToList();
                    var actualVersionCount = packages.Count;

                    var versionsDisplay = string.Join(", ", packages.Select(p => p.Identity.Version));
                    WriteLine($"{actualVersionCount} versions of {packageId} found: {versionsDisplay}");
                    return actualVersionCount == expectedVersionCount;
                }));
        }

        /// <summary>
        /// Searchs the gallery to get the packages matching the specific search term and returns their count.
        /// </summary>
        public static int GetPackageCountForSearchTerm(string searchQuery)
        {
            var repository = Repository.Factory.GetCoreV2(new PackageSource(SourceUrl));
            var resource = repository.GetResource<PackageSearchResource>();
            using var cacheContext = new SourceCacheContext { NoCache = true };
            var packages = resource.SearchAsync(searchQuery, new SearchFilter(includePrerelease: false), skip: 0, take: 1000, log: NullLogger.Instance, cancellationToken: CancellationToken.None).Result;
            return packages.Count();
        }

        /// <summary>
        /// Given the path to the nupkg file, returns the corresponding package ID.
        /// </summary>
        public string GetPackageIdFromNupkgFile(string filePath)
        {
            try
            {
                using var reader = new PackageArchiveReader(filePath);
                var identity = reader.GetIdentity();
                return identity.Id;
            }
            catch (Exception e)
            {
                WriteLine(" Exception thrown while trying to read nupkg for :{0}. Message {1}", filePath, e.Message);
                return null;
            }
        }

        /// <summary>
        /// Given the path to the nupkg file, returns the corresponding package ID.
        /// </summary>
        public bool IsPackageVersionUnListed(string packageId, string version)
        {
            var metadata = GetPackageMetadataAsync(packageId).Result;
            var nugetVersion = NuGetVersion.Parse(version);
            var package = metadata.FirstOrDefault(m => m.Identity.Version == nugetVersion);
            if (package != null)
                return !package.IsListed;
            else
                return false;
        }

        /// <summary>
        /// Clears the local package folder.
        /// </summary>
        public void ClearLocalPackageFolder(string packageId, string version = "1.0.0")
        {
            string expectedDownloadedNupkgFileName = packageId + "." + version;
            string pathToNupkgFolder = Path.Combine(Environment.CurrentDirectory, expectedDownloadedNupkgFileName);
            WriteLine("Path to the downloaded Nupkg file for clearing local package folder is: " + pathToNupkgFolder);
            if (Directory.Exists(pathToNupkgFolder))
                Directory.Delete(expectedDownloadedNupkgFileName, true);
        }

        /// <summary>
        /// Given a package checks if it is installed properly in the current dir.
        /// </summary>
        public bool CheckIfPackageInstalled(string packageId)
        {
            string packageVersion = GetLatestStableVersion(packageId);
            return CheckIfPackageVersionInstalled(packageId, packageVersion);
        }

        /// <summary>
        /// Given a package checks if it that version of the package is installed.
        /// </summary>
        public bool CheckIfPackageVersionInstalled(string packageId, string packageVersion)
        {
            //string packageVersion = ClientSDKHelper.GetLatestStableVersion(packageId);
            string expectedDownloadedNupkgFileName = packageId + "." + packageVersion;
            //check if the nupkg file exists on the expected path post install
            string expectedNupkgFilePath = Path.Combine(Environment.CurrentDirectory, expectedDownloadedNupkgFileName, expectedDownloadedNupkgFileName + ".nupkg");
            WriteLine("The expected Nupkg file path after package install is: " + expectedNupkgFilePath);
            if ((!File.Exists(expectedNupkgFilePath)))
            {
                WriteLine(" Package file {0} not present after download", expectedDownloadedNupkgFileName);
                return false;
            }
            string downloadedPackageId = GetPackageIdFromNupkgFile(expectedNupkgFilePath);
            //Check that the downloaded Nupkg file is not corrupt and it indeed corresponds to the package which we were trying to download.
            if (!(downloadedPackageId.Equals(packageId)))
            {
                WriteLine("Unable to unzip the package downloaded via Nuget Core. Check log for details");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Downloads a package to local folder and see if the download is successful. Used to individual tests which extend the download scenarios.
        /// </summary>
        public void DownloadPackageAndVerify(string packageId, string version = "1.0.0")
        {
            ClearLocalPackageFolder(packageId, version);

            var repository = Repository.Factory.GetCoreV2(new PackageSource(UrlHelper.V2FeedRootUrl));
            var resource = repository.GetResource<FindPackageByIdResource>();
            using var cacheContext = new SourceCacheContext { NoCache = true };

            var expectedDownloadedNupkgFileName = packageId + "." + version;
            var outputDir = Path.Combine(Environment.CurrentDirectory, expectedDownloadedNupkgFileName);
            Directory.CreateDirectory(outputDir);
            var nupkgPath = Path.Combine(outputDir, expectedDownloadedNupkgFileName + ".nupkg");

            using (var fileStream = File.Create(nupkgPath))
            {
                resource.CopyNupkgToStreamAsync(packageId, NuGetVersion.Parse(version), fileStream, cacheContext, NullLogger.Instance, CancellationToken.None).Wait();
            }

            // Verify the downloaded nupkg is a valid package with the expected identity.
            using (var reader = new PackageArchiveReader(nupkgPath))
            {
                var identity = reader.GetIdentity();
                Assert.Equal(packageId, identity.Id);
                Assert.Equal(version, identity.Version.ToNormalizedString());
                Assert.NotEmpty(reader.GetFiles());
            }
        }

        public void CleanCreatedPackage(string packageFullPath)
        {
            if (!string.IsNullOrEmpty(packageFullPath) && File.Exists(packageFullPath))
            {
                File.Delete(packageFullPath);
                Directory.Delete(Path.GetFullPath(Path.GetDirectoryName(packageFullPath)), true);
            }
        }
    }
}