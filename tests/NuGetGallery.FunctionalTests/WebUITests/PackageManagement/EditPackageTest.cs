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

            WebTestRequest verifyEditPostRequest = AssertAndValidationHelper.GetEditPackagePostRequest(this, packageId, "1.0.0", description: "This is a new description.", authors: "clayco", copyright: "Copyright 2014", tags:"Tag1 Tag2", summary:"This is a summary.");
            ValidationRuleFindText newDescriptionValidationRule = AssertAndValidationHelper.GetValidationRuleForFindText(@"This is a new description.");
            ValidationRuleFindText pendingEditValidationRule = AssertAndValidationHelper.GetValidationRuleForFindText(@"An edit is pending for this package version.");          
            verifyEditPostRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(newDescriptionValidationRule.Validate);
            verifyEditPostRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(pendingEditValidationRule.Validate);
            yield return verifyEditPostRequest;
            verifyEditPostRequest = null;

            // wait a minute.
            System.Threading.Thread.Sleep(60000);
            WebTestRequest verifyProcessedRequest = new WebTestRequest(UrlHelper.GetPackagePageUrl(packageId, "1.0.0"));
            ValidationRuleFindText noPendingEditValidationRule = AssertAndValidationHelper.GetValidationRuleForFindText(@"An edit is pending for this package version.", false);
            verifyProcessedRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(newDescriptionValidationRule.Validate);
            verifyProcessedRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(noPendingEditValidationRule.Validate);
            yield return verifyProcessedRequest;
            verifyProcessedRequest = null;

        }
    }
}
