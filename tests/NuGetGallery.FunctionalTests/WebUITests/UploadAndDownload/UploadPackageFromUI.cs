
namespace NuGetGallery.FunctionalTests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.VisualStudio.TestTools.WebTesting;
    using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
    using NuGetGallery.FunctionalTests.TestBase;
    using NuGetGallery.FunctionTests.Helpers;
    using System;
    using System.Collections.Generic;
    using System.Text;
   
    /// <summary>
    /// Uploads a new test package using Gallery UI. Validates that logon prompt appears to upload and checks that the package's home page opens post upload.
    /// </summary>
    public class UploadPackageFromUI : WebTest
    {
        public UploadPackageFromUI()
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

            // The API key is part of the nuget.config file that is present under the solution dir.
            string packageId = DateTime.Now.Ticks.ToString();
            string packageFullPath = PackageCreationHelper.CreatePackage(packageId);

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
