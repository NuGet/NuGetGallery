
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
            uploadRequest = null;

            //Upload a new package.   
            string packageId = this.Name + DateTime.Now.Ticks.ToString();
            string version = "1.0.0";
            string minClientVersion = "2.3";
            string packageFullPath = PackageCreationHelper.CreatePackage(packageId, version, minClientVersion);

            //Do initial login to be able to perform package management.
            logonGet = AssertAndValidationHelper.GetLogonGetRequest();
            yield return logonGet;
            logonGet = null;
            logonPost = AssertAndValidationHelper.GetLogonPostRequest(this);
            yield return logonPost;
            logonPost = null;

            System.Threading.Thread.Sleep(60000);
            WebTestRequest packageRequest = new WebTestRequest(UrlHelper.GetPackagePageUrl(packageId));           
            //Rule to check manage my packages contains a html link to the newly uploaded package.     
            ValidationRuleFindText requiredMinVersionValidationRule = AssertAndValidationHelper.GetValidationRuleForFindText(minClientVersion);   
            packageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(requiredMinVersionValidationRule.Validate);
            yield return packageRequest;
            packageRequest = null;         

        }
    }
}
