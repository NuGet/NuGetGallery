using FluentAutomation;
using NuGetGallery.FunctionalTests.Helpers;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.Net;

namespace NuGetGallery.FunctionalTests.Fluent
{
    public class NuGetFluentTest : FluentTest 
    {
        private static bool _checkForPackageExistence = false;
        public NuGetFluentTest()
        {
            FluentAutomation.SeleniumWebDriver.Bootstrap();
        }

        public void UploadPackageIfNecessary(string packageName, string version)
        {
            if (!PackageExists(packageName, version))
            {
                AssertAndValidationHelper.UploadNewPackageAndVerify(packageName, version);
            }
        }

        public void UploadPackageIfNecessary(string packageName, string version, string minClientVersion, string title, string tags, string description)
        {
            if (!PackageExists(packageName, version, UrlHelper.V2FeedRootUrl))
            {
                AssertAndValidationHelper.UploadNewPackageAndVerify(packageName, version, minClientVersion, title, tags, description);
            }
        }

        public void UploadPackageIfNecessary(string packageName, string version, string minClientVersion, string title, string tags, string description, string licenseUrl)
        {
            if (!PackageExists(packageName, version, UrlHelper.V2FeedRootUrl))
            {
                AssertAndValidationHelper.UploadNewPackageAndVerify(packageName, version, minClientVersion, title, tags, description, licenseUrl);
            }
        }

        public void UploadPackageIfNecessary(string packageName, string version, string minClientVersion, string title, string tags, string description, string licenseUrl, string dependencies)
        {
            if (!PackageExists(packageName, version, UrlHelper.V2FeedRootUrl))
            {
                AssertAndValidationHelper.UploadNewPackageAndVerify(packageName, version, minClientVersion, title, tags, description, licenseUrl, dependencies);
            }
        }

        public bool PackageExists(string packageName, string version)
        {
            bool found = false;
            for (int i = 0; ((i < 6) && (!found)); i++)
            {
                string requestURL = UrlHelper.V2FeedRootUrl + @"package/" + packageName + "/" + version + "?t=" + DateTime.Now.Ticks;
                Console.WriteLine("The request URL for checking package existence was: " + requestURL);
                HttpWebRequest packagePageRequest = (HttpWebRequest)HttpWebRequest.Create(requestURL);

                // Increase timeout to be consistent with the functional tests
                packagePageRequest.Timeout = 2 * 5000;
                HttpWebResponse packagePageResponse;
                try
                {               
                    packagePageResponse = (HttpWebResponse)packagePageRequest.GetResponse();
                    if (packagePageResponse != null && (((HttpWebResponse)packagePageResponse).StatusCode == HttpStatusCode.OK)) found = true;
                }
                catch (WebException e)
                {
                    Console.WriteLine(e.Message);
                }
            }
            return found;
        }

        public bool PackageExists(string packageName, string version, string url)
        {
            return ClientSDKHelper.CheckIfPackageVersionExistsInSource(packageName, version, url);
        }

        public static bool CheckForPackageExistence
        {
            get
            {
                {
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CheckforPackageExistence")))
                        _checkForPackageExistence = false;
                    else
                        _checkForPackageExistence = Convert.ToBoolean(Environment.GetEnvironmentVariable("CheckforPackageExistence"));
                }
                return _checkForPackageExistence;
            }
        }
    }
}
