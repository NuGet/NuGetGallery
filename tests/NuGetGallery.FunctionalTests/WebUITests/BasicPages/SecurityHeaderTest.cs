﻿using Microsoft.VisualStudio.TestTools.WebTesting;
using NuGetGallery.FunctionalTests.Helpers;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.Collections.Generic;

namespace NuGetGallery.FunctionalTests
{
     /// <summary>
     ///     Verify that an expected series of security headers is returned as part of the response.
     ///     Priority :P2
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
             homePageRequest.ParseDependentRequests = false;
             ValidationRuleFindHeaderText homePageTextValidationRule = new ValidationRuleFindHeaderText(
 @"X-Frame-Options: deny
X-XSS-Protection: 1; mode=block
X-Content-Type-Options: nosniff
Strict-Transport-Security: max-age=31536000; includeSubDomains");               
             homePageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(homePageTextValidationRule.Validate);         
             yield return homePageRequest;
             homePageRequest = null;
 
             //send a request to Packages page and check for security headers.
             WebTestRequest packagesPageRequest = new WebTestRequest(UrlHelper.PackagesPageUrl);
             packagesPageRequest.ParseDependentRequests = false;
             ValidationRuleFindHeaderText packagesPageTextValidationRule = new ValidationRuleFindHeaderText(
 @"X-Frame-Options: deny
X-XSS-Protection: 1; mode=block
X-Content-Type-Options: nosniff
Strict-Transport-Security: max-age=31536000; includeSubDomains");    
            packagesPageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(packagesPageTextValidationRule.Validate);
            yield return packagesPageRequest;
            packagesPageRequest = null;


        }
    }
}
