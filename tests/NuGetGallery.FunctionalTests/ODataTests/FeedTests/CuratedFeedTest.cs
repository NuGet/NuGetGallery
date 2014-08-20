using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.Collections;
using System.IO;
using System.Net;

namespace NuGetGallery.FunctionalTests.Features
{
    [TestClass]
    public class CuratedFeedTest
    {
        [TestMethod]
        [Description("Performs a querystring-based search of the Microsoft curated feed. Confirms expected packages are returned.")]
        [Priority(0)]
        public void SearchMicrosoftDotNetCuratedFeed()
        {
            string packageId = "microsoft.aspnet.webpages";
            WebRequest request = WebRequest.Create(UrlHelper.DotnetCuratedFeedUrl + @"Packages()?$filter=tolower(Id)%20eq%20'" + packageId + "'&$orderby=Id&$skip=0&$top=30");
            // Get the response.          
            WebResponse response = request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd();
            string packageURL = @"<id>" + UrlHelper.DotnetCuratedFeedUrl + "Packages(Id='" + packageId;
            Assert.IsTrue(responseText.ToLowerInvariant().Contains(packageURL.ToLowerInvariant()));
        }

        // This test fails due to the following error
        // The package upload via Nuget.exe didnt succeed properly. Could not establish trust relationship for the SSL/TLS secure channel
        [TestMethod]
        [Description("Performs a querystring-based search of the Windows 8 curated feed. Confirms expected packages are returned.")]
        [Priority(0)]
        public void SearchWindows8CuratedFeed()
        {
            // Temporary workaround for the SSL issue, which keeps the upload test from working with cloudapp.net sites
            if (UrlHelper.BaseUrl.Contains("nugettest.org") || UrlHelper.BaseUrl.Contains("nuget.org"))
            {
                string packageName = "NuGetGallery.FunctionalTests.SearchWindows8CuratedFeed";
                string ticks = DateTime.Now.Ticks.ToString();
                string version = new System.Version(ticks.Substring(0, 6) + "." + ticks.Substring(6, 6) + "." + ticks.Substring(12, 6)).ToString();

                int exitCode = UploadPackageToCuratedFeed(packageName, version, FeedType.Windows8CuratedFeed);
                Assert.IsTrue((exitCode == 0), Constants.UploadFailureMessage);

                bool applied = CheckPackageExistInCuratedFeed(packageName, FeedType.Windows8CuratedFeed);
                Assert.IsTrue(applied, Constants.PackageNotFoundAfterUpload, packageName, UrlHelper.Windows8CuratedFeedUrl);
            }
        }

        // This test fails due to the following error
        // The package upload via Nuget.exe didnt succeed properly. Could not establish trust relationship for the SSL/TLS secure channel
        [TestMethod]
        [Description("Performs a querystring-based search of the WebMatrix curated feed.  Confirms expected packages are returned.")]
        [Priority(0)]
        public void SearchWebMatrixCuratedFeed()
        {
            if (UrlHelper.BaseUrl.Contains("nugettest.org") || UrlHelper.BaseUrl.Contains("nuget.org"))
            {
                string packageName = "NuGetGallery.FunctionalTests.SearchWebMatrixCuratedFeed";
                string ticks = DateTime.Now.Ticks.ToString();
                string version = new System.Version(ticks.Substring(0, 6) + "." + ticks.Substring(6, 6) + "." + ticks.Substring(12, 6)).ToString();

                int exitCode = UploadPackageToCuratedFeed(packageName, version, FeedType.WebMatrixCuratedFeed);
                Assert.IsTrue((exitCode == 0), Constants.UploadFailureMessage);

                bool applied = CheckPackageExistInCuratedFeed(packageName, FeedType.WebMatrixCuratedFeed);
                Assert.IsTrue(applied, Constants.PackageNotFoundAfterUpload, packageName, UrlHelper.WebMatrixCuratedFeedUrl);
            }
        }

        [TestMethod]
        [Description("Checks the MicrosoftDotNet curated feed for duplicate packages.")]
        [Priority(1)]
        public void CheckMicrosoftDotNetCuratedFeedForDuplicates()
        {
            CheckCuratedFeedForDuplicates(FeedType.DotnetCuratedFeed); 
        }

        //[TestMethod]
        [Description("Checks the WebMatrix curated feed for duplicate packages.")]
        [Priority(1)]
        [Ignore] //This method is marked ignore as it takes a very long time to run. It can be run manually if required.
        public void CheckWebMatrixCuratedFeedForDuplicates()
        {
            CheckCuratedFeedForDuplicates(FeedType.WebMatrixCuratedFeed);
        }

        [TestMethod]
        [Description("Checks the Windows8 curated feed for duplicate packages.")]
        [Priority(1)]
        public void CheckWindows8CuratedFeedForDuplicates()
        {
            CheckCuratedFeedForDuplicates(FeedType.Windows8CuratedFeed);
        }

        [TestMethod]
        [Description("Validates the microsoftdotnet feed, including the next page link")]
        [Priority(1)]
        public void ValidateMicrosoftDotNetCuratedFeed()
        {
            WebRequest request = WebRequest.Create(GetCuratedFeedUrl(FeedType.DotnetCuratedFeed) + "Packages");

            // Get the response.          
            WebResponse response = request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd();

            // Make sure that 40 entries are returned.  This means that if we split on the <entry> tag, we'd have 41 strings.
            int length = responseText.Split(new string[] { "<entry>" }, StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.IsTrue(length == 41, "An unexpected number of entries was found.  Actual number was " + (length - 1));

            // Get the link to the next page.
            string link = responseText.Split(new string[] { @"<link rel=""next"" href=""" }, StringSplitOptions.RemoveEmptyEntries)[1];
            link = link.Substring(0, link.IndexOf(@""""));

            request = WebRequest.Create(link);

            // Get the response.          
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException e)
            {
                if (((HttpWebResponse)e.Response).StatusCode != HttpStatusCode.OK) Assert.Fail("Next page link is broken.  Expected 200, got " + ((HttpWebResponse)e.Response).StatusCode);
            }
        }

        #region Helper Methods
        public int UploadPackageToCuratedFeed(string packageName, string version, FeedType feedType)
        {
            string packageFullPath = string.Empty;
            switch (feedType)
            {
                case FeedType.Windows8CuratedFeed:
                    packageFullPath = PackageCreationHelper.CreateWindows8CuratedPackage(packageName, version);
                    break;
                case FeedType.WebMatrixCuratedFeed:
                    packageFullPath = PackageCreationHelper.CreateWindows8CuratedPackage(packageName, version);
                    break;
            }
            int exitCode = CmdLineHelper.UploadPackage(packageFullPath, UrlHelper.V2FeedPushSourceUrl);
            return exitCode;
        }

        public string GetCuratedFeedUrl(FeedType type)
        {
            string url = string.Empty;
            switch (type)
            {
                case FeedType.Windows8CuratedFeed:
                    url = UrlHelper.Windows8CuratedFeedUrl;
                    break;
                case FeedType.WebMatrixCuratedFeed:
                    url = UrlHelper.WebMatrixCuratedFeedUrl;
                    break;
                case FeedType.DotnetCuratedFeed:
                    url = UrlHelper.DotnetCuratedFeedUrl;
                    break;
            }
            return url;
        }

        public bool CheckPackageExistInCuratedFeed(string packageName, FeedType feedType)
        {
            string curatedFeedUrl = GetCuratedFeedUrl(feedType);
            bool applied = false;
            for (int i = 0; i < 10 && applied == false; i++)
            {
                System.Threading.Thread.Sleep(30 * 1000);
                if (ClientSDKHelper.CheckIfPackageExistsInSource(packageName, curatedFeedUrl))
                {
                    applied = true;
                }
            }
            return applied;
        }

        public void CheckCuratedFeedForDuplicates(FeedType feedType)
        {
            WebRequest request = WebRequest.Create(GetCuratedFeedUrl(feedType) + "Packages");
            request.Timeout = 15000;
            ArrayList packages = new ArrayList();

            // Get the response.          
            WebResponse response = request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd();
            responseText = responseText.Substring(responseText.IndexOf("<entry>"));
            CheckPageForDuplicates(packages, responseText);

            while (responseText.Contains(@"<link rel=""next"" href="""))
            {
                // Get the link to the next page.
                string link = responseText.Split(new string[] { @"<link rel=""next"" href=""" }, StringSplitOptions.RemoveEmptyEntries)[1];
                link = link.Substring(0, link.IndexOf(@""""));

                request = WebRequest.Create(link);
                request.Timeout = 2000;

                // Get the response.          
                try
                {
                    response = (HttpWebResponse)request.GetResponse();
                    sr = new StreamReader(response.GetResponseStream());
                    responseText = sr.ReadToEnd();
                    responseText = responseText.Substring(responseText.IndexOf("<entry>"));
                    CheckPageForDuplicates(packages, responseText);
                }
                catch (WebException e)
                {
                    if (((HttpWebResponse)e.Response).StatusCode != HttpStatusCode.OK) Assert.Fail("Next page link is broken.  Expected 200, got " + ((HttpWebResponse)e.Response).StatusCode);
                }
            }
        }

        private static string CheckPageForDuplicates(ArrayList packages, string responseText)
        {
            string unreadPortion = responseText;

            while (unreadPortion.Contains("<id>"))
            {
                unreadPortion = unreadPortion.Substring(unreadPortion.IndexOf("<id>") + 4);
                string packageIdString = unreadPortion.Substring(0, unreadPortion.IndexOf("</id>"));
                if (packages.Contains(packageIdString))
                {
                    Assert.Fail("A package appeared twice in the WebMatrix feed: " + packageIdString);
                }
                else
                {
                    packages.Add(packageIdString);
                }
                unreadPortion = unreadPortion.Substring(1);
            }
            return unreadPortion;
        }
        #endregion
    }
}
