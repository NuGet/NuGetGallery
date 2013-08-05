namespace NuGetGallery.FunctionalTests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.WebTesting;
    using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
    using NuGetGallery.FunctionTests.Helpers;

    /// <summary>
    /// Sends http request to gallery home page checks for the default home page text in the reponse.
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
