using Microsoft.VisualStudio.TestTools.WebTesting;
using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
using NuGetGallery.FunctionalTests.Helpers;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.Collections.Generic;

namespace NuGetGallery.FunctionalTests
{
    /// <summary>
    /// Sends http request to gallery home page checks for the default home page text in the reponse.
    /// priority : p0
    /// </summary>
    public class HomePageValidationTest : WebTest
    {
        public HomePageValidationTest()
        {
            this.PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            //send a request to home page and check for default home page text.
            WebTestRequest homePageRequest = new WebTestRequest(UrlHelper.BaseUrl);           
            ValidationRuleFindText homePageTextValidationRule = AssertAndValidationHelper.GetValidationRuleForFindText(Constants.HomePageText);               
            homePageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(homePageTextValidationRule.Validate);         
            yield return homePageRequest;
            homePageRequest = null;          
        }
    }
}
