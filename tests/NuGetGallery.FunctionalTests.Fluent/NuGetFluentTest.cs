using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGetGallery.FunctionTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAutomation;


namespace NuGetGallery.FunctionalTests.Fluent
{
    public class NuGetFluentTest : FluentTest 
    {
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
            if (!PackageExists(packageName, version))
            {
                AssertAndValidationHelper.UploadNewPackageAndVerify(packageName, version, minClientVersion, title, tags, description);
            }
        }

        public void UploadPackageIfNecessary(string packageName, string version, string minClientVersion, string title, string tags, string description, string licenseUrl)
        {
            if (!PackageExists(packageName, version))
            {
                AssertAndValidationHelper.UploadNewPackageAndVerify(packageName, version, minClientVersion, title, tags, description, licenseUrl);
            }
        }

        public void UploadPackageIfNecessary(string packageName, string version, string minClientVersion, string title, string tags, string description, string licenseUrl, string dependencies)
        {
            if (!PackageExists(packageName, version))
            {
                AssertAndValidationHelper.UploadNewPackageAndVerify(packageName, version, minClientVersion, title, tags, description, licenseUrl, dependencies);
            }
        }

        public bool PackageExists(string packageName, string version)
        {
            bool found = false;
            for (int i = 0; ((i < 30) && (!found)); i++)
            {
                HttpWebRequest packagePageRequest = (HttpWebRequest)HttpWebRequest.Create(UrlHelper.V2FeedRootUrl + @"/package/" + packageName + "/" + version);
                packagePageRequest.Timeout = 1000;
                HttpWebResponse packagePageResponse;
                try
                {
                    packagePageResponse = (HttpWebResponse)packagePageRequest.GetResponse();
                    if (packagePageResponse != null && (((HttpWebResponse)packagePageResponse).StatusCode == HttpStatusCode.OK)) found = true;
                }
                catch (WebException e)
                {
                    return false;
                }
            }
            return found;
        }
    }
}
