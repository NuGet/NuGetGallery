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
            ValidationRuleFindText homePageTextValidationRuleLogo = AssertAndValidationHelper.GetValidationRuleForFindText(@"<a href=""http://outercurve.org""><img src=""/Content/Logos/outercurve.png"" alt=""Outercurve Foundation"" /></a>");               
            ValidationRuleFindText homePageTextValidationRuleCopyright = AssertAndValidationHelper.GetValidationRuleForFindText(@"&copy; 2013 Outercurve Foundation");
            ValidationRuleFindText homePageTextValidationRuleTOU = AssertAndValidationHelper.GetValidationRuleForFindText(@"<a href=""/policies/Terms"">Terms of Use</a>");
            ValidationRuleFindText homePageTextValidationRulePrivacy = AssertAndValidationHelper.GetValidationRuleForFindText(@"<a href=""/policies/Privacy"">Privacy Policy</a>");
            pageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(homePageTextValidationRuleLogo.Validate);
            pageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(homePageTextValidationRuleCopyright.Validate);
            pageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(homePageTextValidationRuleTOU.Validate);
            pageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(homePageTextValidationRulePrivacy.Validate);
            yield return pageRequest;
            pageRequest = null;
        }
    }
}

