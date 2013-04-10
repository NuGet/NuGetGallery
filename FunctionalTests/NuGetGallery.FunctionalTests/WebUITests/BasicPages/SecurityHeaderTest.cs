namespace NuGetGallery.FunctionalTests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.WebTesting;
    using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
    using NuGetGallery.FunctionTests.Helpers;

    /// <summary>
    ///     Verify that an expected series of security headers is returned as part of the response.
    /// </summary>
    public class SecurityHeaderTest : WebTest
    {
        public SecurityHeaderTest()
        {
            this.PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            //send a request to home page and check for security headers.
            WebTestRequest homePageRequest = new WebTestRequest(UrlHelper.BaseUrl);           
            ValidationRuleFindHeaderText homePageTextValidationRule = new ValidationRuleFindHeaderText(
@"X-Frame-Options: deny
X-XSS-Protection: 1; mode=block
X-Content-Type-Options: nosniff
Strict-Transport-Security: maxage=31536000; includeSubDomains");               
            homePageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(homePageTextValidationRule.Validate);         
            yield return homePageRequest;
            homePageRequest = null;

            //send a request to Packages page and check for security headers.
            WebTestRequest packagesPageRequest = new WebTestRequest(UrlHelper.PackagesPageUrl);
            ValidationRuleFindHeaderText packagesPageTextValidationRule = new ValidationRuleFindHeaderText(
@"X-Frame-Options: deny
X-XSS-Protection: 1; mode=block
X-Content-Type-Options: nosniff
Strict-Transport-Security: maxage=31536000; includeSubDomains");    
            packagesPageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(packagesPageTextValidationRule.Validate);
            yield return packagesPageRequest;
            packagesPageRequest = null;


        }
    }
}
