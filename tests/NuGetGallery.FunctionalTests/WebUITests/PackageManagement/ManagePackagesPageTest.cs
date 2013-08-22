
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
    public class ManagePackagesPageTest : WebTest
    {
        public ManagePackagesPageTest()
        {
            this.PreAuthenticate = true;
        }
        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            //Upload a new package.   
            string packageId = this.Name + DateTime.Now.Ticks.ToString();
            string version = "1.0.0";
            AssertAndValidationHelper.UploadNewPackageAndVerify(packageId, version);

            //Do initial login to be able to perform package management.
            WebTestRequest logonGet = AssertAndValidationHelper.GetLogonGetRequest();
            yield return logonGet;
            logonGet = null;
            WebTestRequest logonPost = AssertAndValidationHelper.GetLogonPostRequest(this);
            yield return logonPost;
            logonPost = null;

            WebTestRequest managePackagesRequest = new WebTestRequest(UrlHelper.ManageMyPackagesUrl);           
            //Rule to check manage my packages contains a html link to the newly uploaded package.     
            ValidationRuleFindText newPackageIdValidationRule = AssertAndValidationHelper.GetValidationRuleForFindText(packageId);   
            managePackagesRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(newPackageIdValidationRule.Validate);
            yield return managePackagesRequest;
            managePackagesRequest = null;         

        }
    }
}
