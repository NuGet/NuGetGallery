using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionalTests.Helpers;
using NuGetGallery.FunctionalTests.TestBase;
using NuGetGallery.FunctionTests.Helpers;

namespace NuGetGallery.FunctionalTests.ClientIntegrationTests
{
    /// <summary>
    /// Tries to download and upload package from the gallery using NuGet.exe client.
    /// </summary>
    // All the tests in this class fail due to the following error
    // The package upload via Nuget.exe didnt succeed properly OR package download from V2 feed didnt work. Could not establish trust relationship for the SSL/TLS secure channel
    [TestClass]
    public class NugetCommandLineTests : GalleryTestBase
    {
        [TestMethod]
        [Description("Downloads a package using NuGet.exe and checks if the package file is present in the output dir")]
        [Priority(0)]
        public async Task DownloadPackageWithNuGetCommandLineTest()
        {
            // Temporary work around for the SSL issue, which keeps the upload tests from working on sites with cloudapp.net
            if (UrlHelper.BaseUrl.Contains("nugettest.org") || UrlHelper.BaseUrl.Contains("nuget.org"))
            {
                string packageId = Constants.TestPackageId; //try to down load a pre-defined test package.
                ClientSDKHelper.ClearLocalPackageFolder(packageId);

                CmdLineHelper.ProcessResult result = await CmdLineHelper.InstallPackageAsync(packageId, UrlHelper.V2FeedRootUrl, Environment.CurrentDirectory);

                Assert.IsTrue(result.ExitCode == 0, Constants.PackageDownloadFailureMessage);
                Assert.IsTrue(ClientSDKHelper.CheckIfPackageInstalled(packageId), Constants.PackageInstallFailureMessage);
            }
        }

        [TestMethod]
        [Description("Creates a test package and pushes it to the server using Nuget.exe")]
        [Priority(0)]
        public async Task UploadPackageWithNuGetCommandLineTest()
        {
            if (UrlHelper.BaseUrl.Contains("nugettest.org") || UrlHelper.BaseUrl.Contains("nuget.org"))
            {
                await AssertAndValidationHelper.UploadNewPackageAndVerify(DateTime.Now.Ticks.ToString());
            }
        }

        [TestMethod]
        [Description("Creates a test package with minclientversion tag and .cs name. Pushes it to the server using Nuget.exe and then download via ClientSDK")]
        [Priority(0)]
        public async Task UploadAndDownLoadPackageWithMinClientVersion()
        {
            if (UrlHelper.BaseUrl.Contains("nugettest.org") || UrlHelper.BaseUrl.Contains("nuget.org"))
            {
                string packageId = DateTime.Now.Ticks + "PackageWithDotCsNames.Cs";
                string version = "1.0.0";
                string packageFullPath = await PackageCreationHelper.CreatePackageWithMinClientVersion(packageId, version, "2.3");

                CmdLineHelper.ProcessResult processResult = await CmdLineHelper.UploadPackageAsync(packageFullPath, UrlHelper.V2FeedPushSourceUrl);

                Assert.IsTrue(processResult.ExitCode == 0, Constants.UploadFailureMessage);
                Assert.IsTrue(ClientSDKHelper.CheckIfPackageVersionExistsInSource(packageId, version, UrlHelper.V2FeedRootUrl), Constants.PackageNotFoundAfterUpload, packageId, UrlHelper.V2FeedRootUrl);

                //Delete package from local disk so once it gets uploaded
                if (File.Exists(packageFullPath))
                {
                    File.Delete(packageFullPath);
                    Directory.Delete(Path.GetFullPath(Path.GetDirectoryName(packageFullPath)), true);
                }
                AssertAndValidationHelper.DownloadPackageAndVerify(packageId);
            }
        }
    }
}
