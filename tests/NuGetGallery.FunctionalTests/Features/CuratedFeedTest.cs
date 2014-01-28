﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;
using System.Net;
using System.IO;

namespace NuGetGallery.FunctionalTests.Features
{
    [TestClass]
    public class CuratedFeedTest
    {
        private TestContext testContextInstance;
        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        [TestMethod]
        [Description("Performs a querystring-based search of the Windows 8 curated feed.  Confirms expected packages are returned.")]
        public void SearchWindows8CuratedFeed()
        {
            string packageName = "NuGetGallery.FunctionalTests.SearchWindows8CuratedFeed";
            string ticks = DateTime.Now.Ticks.ToString();
            string version = new System.Version(ticks.Substring(0, 6) + "." + ticks.Substring(6, 6) + "." + ticks.Substring(12, 6)).ToString();
            string packageFullPath = PackageCreationHelper.CreateWindows8CuratedPackage(packageName, version);

            int exitCode = CmdLineHelper.UploadPackage(packageFullPath, UrlHelper.V2FeedPushSourceUrl);
            Assert.IsTrue((exitCode == 0), "The package upload via Nuget.exe didnt suceed properly. Check the logs to see the process error and output stream");

            // The feed could update anytime in the next 5 minutes and this test would be considered a success.
            bool applied = false;
            for (int i = 0; i < 10 && applied == false; i++)
            {
                System.Threading.Thread.Sleep(30000);
                if (ClientSDKHelper.CheckIfPackageExistsInSource(packageName, UrlHelper.Windows8CuratedFeedUrl))
                {
                    applied = true;
                }
            }
            Assert.IsTrue(applied, "Package {0} is not found in the site {1} after uploading.", packageName, UrlHelper.Windows8CuratedFeedUrl);
        }


        [TestMethod]
        [Description("Performs a querystring-based search of the WebMatrix curated feed.  Confirms expected packages are returned.")]
        public void SearchWebMatrixCuratedFeed()
        {
            string packageName = "NuGetGallery.FunctionalTests.SearchWebMatrixCuratedFeed";
            string ticks = DateTime.Now.Ticks.ToString();
            string version = new System.Version(ticks.Substring(0, 6) + "." + ticks.Substring(6, 6) + "." + ticks.Substring(12, 6)).ToString();
            string packageFullPath = PackageCreationHelper.CreateWebMatrixCuratedPackage(packageName, version);

            int exitCode = CmdLineHelper.UploadPackage(packageFullPath, UrlHelper.V2FeedPushSourceUrl);
            Assert.IsTrue((exitCode == 0), "The package upload via Nuget.exe didnt suceed properly. Check the logs to see the process error and output stream");

            // The feed could update anytime in the next 5 minutes and this test would be considered a success.
            bool applied = false;
            for (int i = 0; i < 10 && applied == false; i++)
            {
                System.Threading.Thread.Sleep(30000);
                if (ClientSDKHelper.CheckIfPackageExistsInSource(packageName, UrlHelper.WebMatrixCuratedFeedUrl))
                {
                    applied = true;
                }
            }
            Assert.IsTrue(applied, "Package {0} is not found in the site {1} after uploading.", packageName, UrlHelper.WebMatrixCuratedFeedUrl);
        }

        [TestMethod]
        [Description("Validates the microsoftdotnet feed, including the next page link")]
        public void ValidateMicrosoftDotNetCuratedFeed()
        {
            WebRequest request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"/curated-feeds/microsoftdotnet/Packages");
            
            // Get the response.          
            WebResponse response = request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd();
            
            // Make sure that 40 entries are returned.  This means that if we split on the <entry> tag, we'd have 41 strings.
            int length = responseText.Split(new string[] { "<entry>" }, StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.IsTrue(length == 41, "An unexpected number of entries was found.  Actual number was " + (length - 1));
            
            // Get the link to the next page.
            string link = responseText.Split(new string [] { @"<link rel=""next"" href=""" }, StringSplitOptions.RemoveEmptyEntries)[1];
            link = link.Substring(0, link.IndexOf(@""""));

            request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"/curated-feeds/microsoftdotnet/Packages");

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
    }
}
