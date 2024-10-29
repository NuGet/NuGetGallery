// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
            // Send a request to home page and check for security headers.
            var homePageRequest = new WebTestRequest(UrlHelper.BaseUrl);
            homePageRequest.ParseDependentRequests = false;
            homePageRequest.ValidateResponse += new ValidationRuleFindHeaderText("X-Frame-Options: DENY", StringComparison.OrdinalIgnoreCase).Validate;
            homePageRequest.ValidateResponse += new ValidationRuleFindHeaderText("X-XSS-Protection: 1; mode=block").Validate;
            homePageRequest.ValidateResponse += new ValidationRuleFindHeaderText("X-Content-Type-Options: nosniff").Validate;
            homePageRequest.ValidateResponse += new ValidationRuleFindHeaderText("Strict-Transport-Security: max-age=31536000").Validate;
            yield return homePageRequest;

            // Send a request to Packages page and check for security headers.
            var packagesPageRequest = new WebTestRequest(UrlHelper.PackagesPageUrl);
            packagesPageRequest.ParseDependentRequests = false;
            packagesPageRequest.ValidateResponse += new ValidationRuleFindHeaderText("X-Frame-Options: DENY", StringComparison.OrdinalIgnoreCase).Validate;
            packagesPageRequest.ValidateResponse += new ValidationRuleFindHeaderText("X-XSS-Protection: 1; mode=block").Validate;
            packagesPageRequest.ValidateResponse += new ValidationRuleFindHeaderText("X-Content-Type-Options: nosniff").Validate;
            packagesPageRequest.ValidateResponse += new ValidationRuleFindHeaderText("Strict-Transport-Security: max-age=31536000").Validate;
            yield return packagesPageRequest;
        }
    }
}
