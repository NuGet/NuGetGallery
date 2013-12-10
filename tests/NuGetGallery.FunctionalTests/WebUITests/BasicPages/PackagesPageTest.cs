
namespace NuGetGallery.FunctionalTests
{
    using Microsoft.VisualStudio.TestTools.WebTesting;
    using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
    using NuGetGallery.FunctionTests.Helpers;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Web.UI;

    /// <summary>
    /// Sends http request to individual package pages and checks the response for appropriate title and download count.
    /// </summary>
    public class PackagesPageTest : WebTest
    {
        public PackagesPageTest()
        {
            this.PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            // take package names from the data source        
            string packageId = Constants.TestPackageId;
            WebTestRequest packagePageRequest = new WebTestRequest(UrlHelper.BaseUrl + @"/Packages/" + packageId);      
          
            //Rule to check if the title contains the package id and the latest stable version of the package.
            ValidationRuleFindText packageTitleValidationRule = AssertAndValidationHelper.GetValidationRuleForFindText(packageId + " " + ClientSDKHelper.GetLatestStableVersion(packageId));              
            packagePageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(packageTitleValidationRule.Validate);
            //rule to check that the download count is present in the response.
            ValidationRuleFindText downloadCountValidationRule = AssertAndValidationHelper.GetValidationRuleForFindText(ClientSDKHelper.GetFormattedDownLoadStatistics(packageId));                
            packagePageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(downloadCountValidationRule.Validate);
          
            yield return packagePageRequest;
            packagePageRequest = null;            
        }

       
    }
}
