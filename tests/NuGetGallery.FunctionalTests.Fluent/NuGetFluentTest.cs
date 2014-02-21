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

        public bool PackageExists(string packageName, string version)
        {
            HttpWebRequest packagePageRequest = (HttpWebRequest)HttpWebRequest.Create(UrlHelper.BaseUrl + @"Packages/" + packageName + "/" + version);
            HttpWebResponse packagePageResponse;
            try
            {
                packagePageResponse = (HttpWebResponse)packagePageRequest.GetResponse();
            }
            catch (WebException e)
            {
                if (((HttpWebResponse)e.Response).StatusCode == HttpStatusCode.NotFound) return false;
            }

            // If we didn't get an exception, that means thew resource exists.
            return true;
        }
    }
}
