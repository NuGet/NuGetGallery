namespace NuGetGallery.FunctionalTests.WebUITests.PackageManagement
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.VisualStudio.TestTools.WebTesting;
    using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
    using NuGetGallery.FunctionalTests.TestBase;
    using NuGetGallery.FunctionTests.Helpers;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Web.UI;

    /// <summary>
    /// Uploads a new package and check if it shows up in the "Manage my packages" page.
    /// </summary>
    public class UploadPackageWithMinClientVersionFromUITest : WebTest
    {
        public UploadPackageWithMinClientVersionFromUITest()
        {
            this.PreAuthenticate = true;
        }
        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            ExtractHiddenFields defaultExtractionRule = AssertAndValidationHelper.GetDefaultExtractHiddenFields();

            //Do initial login
            WebTestRequest logonGet = AssertAndValidationHelper.GetLogonGetRequest();
            yield return logonGet;
            logonGet = null;

            WebTestRequest logonPost = AssertAndValidationHelper.GetLogonPostRequest(this);
            yield return logonPost;
            logonPost = null;

            WebTestRequest uploadRequest = AssertAndValidationHelper.GetHttpRequestForUrl(UrlHelper.UploadPageUrl);
            yield return uploadRequest;
            if (this.LastResponse.ResponseUri.ToString().Contains("verify-upload"))
            {
                WebTestRequest cancelGet = AssertAndValidationHelper.GetCancelGetRequest();
                yield return cancelGet;
                cancelGet = null;
                uploadRequest = AssertAndValidationHelper.GetHttpRequestForUrl(UrlHelper.UploadPageUrl);
                yield return uploadRequest;
            }
            uploadRequest = null;

            //Upload a new package.   
            string packageId = this.Name + DateTime.Now.Ticks.ToString();
            string version = "1.0.0";
            string minClientVersion = "2.3";
            string packageFullPath = PackageCreationHelper.CreatePackage(packageId, version, minClientVersion);

            WebTestRequest uploadPostRequest = AssertAndValidationHelper.GetUploadPostRequestForPackage(this, packageFullPath);
            yield return uploadPostRequest;
            uploadPostRequest = null;

            WebTestRequest verifyUploadRequest = new WebTestRequest(UrlHelper.VerifyUploadPageUrl);
            verifyUploadRequest.ExtractValues += new EventHandler<ExtractionEventArgs>(defaultExtractionRule.Extract);
            yield return verifyUploadRequest;
            verifyUploadRequest = null;

            WebTestRequest verifyUploadPostRequest = AssertAndValidationHelper.GetVerifyPackagePostRequestForPackage(this, packageId, "1.0.0");
            yield return verifyUploadPostRequest;
            verifyUploadPostRequest = null;

        }
    }
}

