// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.WebTesting;
using NuGetGallery.FunctionalTests.Helpers;

namespace NuGetGallery.FunctionalTests.WebUITests.BasicPages
{
     /// <summary>
     ///     Verify that an expected series of security headers is returned as part of the response.
     ///     Priority :P2
     /// </summary>
     public class SecurityHeaderTest : WebTest
     {
         public SecurityHeaderTest()
         {
             PreAuthenticate = true;
         }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
         {
             //send a request to home page and check for security headers.
             var homePageRequest = new WebTestRequest(UrlHelper.BaseUrl);
             homePageRequest.ParseDependentRequests = false;
             var homePageTextValidationRule = new ValidationRuleFindHeaderText(
 @"X-Frame-Options: deny
X-XSS-Protection: 1; mode=block
X-Content-Type-Options: nosniff
Strict-Transport-Security: max-age=31536000");
             homePageRequest.ValidateResponse += homePageTextValidationRule.Validate;
             yield return homePageRequest;

             //send a request to Packages page and check for security headers.
             var packagesPageRequest = new WebTestRequest(UrlHelper.PackagesPageUrl);
             packagesPageRequest.ParseDependentRequests = false;
             var packagesPageTextValidationRule = new ValidationRuleFindHeaderText(
 @"X-Frame-Options: deny
X-XSS-Protection: 1; mode=block
X-Content-Type-Options: nosniff
Strict-Transport-Security: max-age=31536000");
            packagesPageRequest.ValidateResponse += packagesPageTextValidationRule.Validate;
            yield return packagesPageRequest;
        }
    }
}
