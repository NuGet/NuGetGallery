using Microsoft.VisualStudio.TestTools.WebTesting;
using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
using NuGetGallery.FunctionalTests.Helpers;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.Collections.Generic;

namespace NuGetGallery.FunctionalTests
{   
    /// <summary>
    /// Uploads a new test package using Gallery UI. Validates that logon prompt appears to upload and checks that the package's home page opens post upload.
    /// priority : p0
    /// </summary>
    public class UploadPackageFromUI : WebTest
    {
        public UploadPackageFromUI()
        {
            this.PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            // Temporary workaround for the SSL issue, which keeps the upload test from working with cloudapp.net sites
            if (UrlHelper.BaseUrl.Contains("nugettest.org") || UrlHelper.BaseUrl.Contains("nuget.org"))
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
                    // if there is a upload in progress, try to submit that upload instead of creating a new package (since we are just going to verify that upload goes through UI).
                    //Extract the package Id of the pending upload.
                    string response = this.LastResponse.BodyString;
                    int referenceIndex = response.IndexOf("<h4>Package ID</h4>");
                    int startIndex = response.IndexOf("<p>");
                    int endIndex = response.IndexOf("</p>", startIndex);
                    string packageId = response.Substring(startIndex + 3, endIndex - (startIndex + 3));
                    this.AddCommentToResult(packageId);   //Adding the package ID to result for debugging.             
                    WebTestRequest verifyUploadPostRequest = AssertAndValidationHelper.GetVerifyPackagePostRequestForPackage(this, packageId, "1.0.0", UrlHelper.VerifyUploadPageUrl, Constants.ReadOnlyModeError, 503);
                    yield return verifyUploadPostRequest;
                    verifyUploadPostRequest = null;
                }
                else
                {
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

                    WebTestRequest verifyUploadPostRequest = AssertAndValidationHelper.GetVerifyPackagePostRequestForPackage(this, packageId, "1.0.0", UrlHelper.GetPackagePageUrl(packageId, "1.0.0"), packageId);
                    yield return verifyUploadPostRequest;
                    verifyUploadPostRequest = null;
                }
            }
        }
    }
}
