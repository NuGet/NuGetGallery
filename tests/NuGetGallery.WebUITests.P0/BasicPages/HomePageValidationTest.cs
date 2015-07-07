// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.WebTesting;
using NuGetGallery.FunctionalTests.Helpers;

namespace NuGetGallery.FunctionalTests.WebUITests.BasicPages
{
    /// <summary>
    /// Sends http request to gallery home page checks for the default home page text in the reponse.
    /// priority : p0
    /// </summary>
    public class HomePageValidationTest : WebTest
    {
        public HomePageValidationTest()
        {
            PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            // Send a request to home page and check for default home page text.
            var homePageRequest = new WebTestRequest(UrlHelper.BaseUrl);
            var homePageTextValidationRule = AssertAndValidationHelper.GetValidationRuleForFindText(Constants.HomePageText);
            homePageRequest.ValidateResponse += homePageTextValidationRule.Validate;
            yield return homePageRequest;
        }
    }
}
