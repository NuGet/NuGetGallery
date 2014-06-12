using Microsoft.VisualStudio.TestTools.WebTesting;
using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
using NuGetGallery.FunctionalTests.Helpers;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.Collections.Generic;

namespace NuGetGallery.FunctionalTests
{
    /// <summary>
    /// Sends http request to individual package pages and checks the response for appropriate title and download count.
    /// priority : p1
    /// </summary>
    public class PackagesPageTest : WebTest
    {
        public PackagesPageTest()
        {
            this.PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            //Use a predefined test package.
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
