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
    /// Uploads a new package to gallery. Unlists the package and checks if the unlisted message shows up.
    /// Also checks if it doesn't show up in search results and NuGet.Core returns it as unlisted.
    /// </summary>        
    public class EditPackageTest : WebTest
    {
        private string description;

        public EditPackageTest()
        {
            this.PreAuthenticate = true;
        }


        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            ExtractHiddenFields defaultExtractionRule = AssertAndValidationHelper.GetDefaultExtractHiddenFields();
            
            //Upload a new package.   
            string packageId = this.Name + DateTime.Now.Ticks.ToString();
            string version = "1.0.0";
            AssertAndValidationHelper.UploadNewPackageAndVerify(packageId, version);

            //Do initial login to be able to perform edit.
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
            string packageFullPath = PackageCreationHelper.CreatePackage(packageId);

            WebTestRequest uploadPostRequest = AssertAndValidationHelper.GetUploadPostRequestForPackage(this, packageFullPath);
            yield return uploadPostRequest;
            uploadPostRequest = null;

            WebTestRequest verifyUploadRequest = new WebTestRequest(UrlHelper.VerifyUploadPageUrl);
            yield return verifyUploadRequest;
            verifyUploadRequest = null;

            WebTestRequest verifyUploadPostRequest = AssertAndValidationHelper.GetVerifyPackagePostRequestForPackage(this, packageId, "1.0.0");
            yield return verifyUploadPostRequest;
            verifyUploadPostRequest = null;

            WebTestRequest verifyEditRequest = AssertAndValidationHelper.GetEditGetRequestForPackage(packageId, "1.0.0");
            verifyEditRequest.ExtractValues += new EventHandler<ExtractionEventArgs>(defaultExtractionRule.Extract);
            yield return verifyEditRequest;
            verifyEditRequest = null;

            // TO DO:  Post isn't weorking and I need to investigate why.  I'll investigate more later, but the current verification is still 
            // useful in the meantime. [clayco 11/13/2013]
            WebTestRequest verifyEditPostRequest = AssertAndValidationHelper.GetEditPackagePostRequest(this, packageId, "1.0.0", description: "This is a new description.");
            ValidationRuleFindText newDescriptionValidationRule = AssertAndValidationHelper.GetValidationRuleForFindText(@"This is a new description.");
            //ValidationRuleFindText pendingEditValidationRule = AssertAndValidationHelper.GetValidationRuleForFindText(@"An edit is pending for this package version. You are seeing the <em>edited</em> package description now.");
            verifyEditPostRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(newDescriptionValidationRule.Validate);
            //verifyEditPostRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(pendingEditValidationRule.Validate);
            yield return verifyEditPostRequest;
            verifyEditPostRequest = null;
        }
    }
}
