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
    public class DeletePackageTest : WebTest
    {

        public DeletePackageTest()
        {
            this.PreAuthenticate = true;
        }

        
        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            //Upload a new package.   
            string packageId = this.Name + DateTime.Now.Ticks.ToString();
            string version = "1.0.0";
            AssertAndValidationHelper.UploadNewPackageAndVerify(packageId,version);

            //Do initial login to be able to perform delete ops.
            WebTestRequest logonGet = AssertAndValidationHelper.GetLogonGetRequest();
            yield return logonGet;
            logonGet = null;
            WebTestRequest logonPost = AssertAndValidationHelper.GetLogonPostRequest(this);
            yield return logonPost;
            logonPost = null;

            string deletePackagePageUrl = UrlHelper.GetPackageDeletePageUrl(packageId, version);


            WebTestRequest deletePackageRequest = AssertAndValidationHelper.GetHttpRequestForUrl(deletePackagePageUrl);           
            yield return deletePackageRequest;
            deletePackageRequest = null;

            WebTestRequest deletePackagePagePostRequest = new WebTestRequest(deletePackagePageUrl);
            deletePackagePagePostRequest.Method = "POST";
            //once the listing is done, it should return back to the packages page.
            deletePackagePagePostRequest.ExpectedResponseUrl = UrlHelper.GetPackagePageUrl(packageId, version);
            FormPostHttpBody deletePackagePostRequestForm = new FormPostHttpBody();
            deletePackagePostRequestForm.FormPostParameters.Add("__RequestVerificationToken", this.Context["$HIDDEN1.__RequestVerificationToken"].ToString());
            deletePackagePostRequestForm.FormPostParameters.Add("Listed", this.Context["$HIDDEN1.Listed"].ToString());
            deletePackagePagePostRequest.Body = deletePackagePostRequestForm;

            //Make sure that the package page shows the message saying that it has been unlisted.
            ValidationRuleFindText findTextRule = new ValidationRuleFindText();
            findTextRule.FindText = Constants.UnListedPackageText;
            deletePackagePagePostRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(findTextRule.Validate);
            yield return deletePackagePagePostRequest;
            deletePackagePagePostRequest = null;

            //check if it shows up in search and cross check with client SDK.            
            Assert.IsTrue(ClientSDKHelper.IsPackageVersionUnListed(packageId, version), "Package not returned as unlisted by Nuget core after unlisting it in gallery");

           


        }
    }
}
