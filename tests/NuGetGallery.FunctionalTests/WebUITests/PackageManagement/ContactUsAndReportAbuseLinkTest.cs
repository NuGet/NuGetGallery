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
    public class ContactUsAndReportAbuseLinkTest : WebTest
    {
        public ContactUsAndReportAbuseLinkTest()
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

            string packageId = DateTime.Now.Ticks.ToString();
            string packageFullPath = PackageCreationHelper.CreatePackage(packageId);

            WebTestRequest uploadPostRequest = AssertAndValidationHelper.GetUploadPostRequestForPackage(this, packageFullPath);
            yield return uploadPostRequest;
            uploadPostRequest = null;

            WebTestRequest verifyUploadPostRequest = AssertAndValidationHelper.GetVerifyPackagePostRequestForPackage(this, packageId, "1.0.0");
            yield return verifyUploadPostRequest;
            verifyUploadPostRequest = null; 

            System.Threading.Thread.Sleep(60000);
            WebTestRequest packageRequest = new WebTestRequest(UrlHelper.GetPackagePageUrl(packageId));    
            ValidationRuleFindText findTextRule = AssertAndValidationHelper.GetValidationRuleForFindText("Contact Us");
            packageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(findTextRule.Validate);
            yield return packageRequest;
            packageRequest = null;

            // Log off
            WebTestRequest logOffGet = AssertAndValidationHelper.GetLogOffGetRequest();
            yield return logOffGet;
            logOffGet = null;

            packageRequest = new WebTestRequest(UrlHelper.GetPackagePageUrl(packageId));
            //Rule to check manage my packages contains a html link to the newly uploaded package.     
            ValidationRuleFindText reportAbuseValidationRule = AssertAndValidationHelper.GetValidationRuleForFindText("Report Abuse");
            packageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(reportAbuseValidationRule.Validate);
            yield return packageRequest;
            packageRequest = null;   

        }
    }
}
