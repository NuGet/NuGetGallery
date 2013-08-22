using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.WebTesting;
using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
using NuGetGallery.FunctionalTests.TestBase;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace NuGetGallery.FunctionalTests.WebUITests.PackageManagement
{
    public class ContactOwnerTest : WebTest
    {
        public ContactOwnerTest()
        {
            this.PreAuthenticate = true;
        }
        
        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            //Upload a new package.   
            string packageId = this.Name + DateTime.Now.Ticks.ToString();
            string version = "1.0.0";
            AssertAndValidationHelper.UploadNewPackageAndVerify(packageId,version);

            //Do initial login to be able to contact owner.
            WebTestRequest logonGet = AssertAndValidationHelper.GetLogonGetRequest();
            yield return logonGet;
            logonGet = null;
            WebTestRequest logonPost = AssertAndValidationHelper.GetLogonPostRequest(this);
            yield return logonPost;
            logonPost = null;

            WebTestRequest conactOwnerRequest = AssertAndValidationHelper.GetHttpRequestForUrl(UrlHelper.GetContactOwnerPageUrl(packageId));         
            yield return conactOwnerRequest;
            conactOwnerRequest = null;

            WebTestRequest conactOwnerPostRequest = new WebTestRequest(UrlHelper.GetContactOwnerPageUrl(packageId));
            conactOwnerPostRequest.Method = "POST";
            //once the listing is done, it should return back to the packages page.
            conactOwnerPostRequest.ExpectedResponseUrl = UrlHelper.GetPackagePageUrl(packageId,version);
            FormPostHttpBody contactOwnerRequestBody = new FormPostHttpBody();
            contactOwnerRequestBody.FormPostParameters.Add("__RequestVerificationToken", this.Context["$HIDDEN1.__RequestVerificationToken"].ToString());
            contactOwnerRequestBody.FormPostParameters.Add("Message", "Test");
            conactOwnerPostRequest.Body = contactOwnerRequestBody;

            //Make sure that the package page shows the message saying that the mail has been sent.
            ValidationRuleFindText findTextRule = AssertAndValidationHelper.GetValidationRuleForFindText(Constants.ContactOwnersText + packageId);            
            conactOwnerPostRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(findTextRule.Validate);
            yield return conactOwnerPostRequest;
            conactOwnerPostRequest = null;

            //Wait for a 30 sec to make sure that the mail reaches properly.
            System.Threading.Thread.Sleep(30 * 1000);

            //Cross check with the pop3 client to check if the message actually has been received.
            string subject = string.Empty;
            Assert.IsTrue(MailHelper.IsMailSentForContactOwner(packageId,out subject), "Contact owners message not sent to the owner properly. Actual subject : {0}", subject);

           


        }
    }
}
