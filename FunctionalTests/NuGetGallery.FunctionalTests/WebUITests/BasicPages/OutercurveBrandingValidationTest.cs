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
    public class OutercurveBrandingValidationTest : WebTest
    {
        public OutercurveBrandingValidationTest()
        {
            this.PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            //send a request to home page and check for default home page text.
            WebTestRequest pageRequest = new WebTestRequest(UrlHelper.BaseUrl);           
            ValidationRuleFindText homePageTextValidationRule = AssertAndValidationHelper.GetValidationRuleForFindText(@"<a href=""http://outercurve.org""><img src=""/Content/Images/outercurve.png"" alt=""Outercurve Foundation"" /></a>");               
            ValidationRuleFindText homePageTextValidationRule2 = AssertAndValidationHelper.GetValidationRuleForFindText(@"&copy; 2013 Outercurve Foundation.");               
            pageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(homePageTextValidationRule.Validate);     
            pageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(homePageTextValidationRule2.Validate);  
            yield return pageRequest;
            pageRequest = null;      
    
            pageRequest = new WebTestRequest(UrlHelper.BaseUrl + "/ThisDoesNotExist");
            pageRequest.ExpectedHttpStatusCode = 404;
            pageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(homePageTextValidationRule.Validate);  
            pageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(homePageTextValidationRule2.Validate);  
            yield return pageRequest;
            pageRequest = null;     
        }
    }
}
