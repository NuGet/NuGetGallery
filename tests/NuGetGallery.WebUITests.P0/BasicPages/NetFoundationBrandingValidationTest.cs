// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.WebTesting;
using NuGetGallery.FunctionalTests.Helpers;

namespace NuGetGallery.FunctionalTests.WebUITests.BasicPages
{
    /// <summary>
    /// Sends http request to gallery home page checks for the default home page text in the reponse.
    /// priority : p1
    /// </summary>
    public class NetFoundationBrandingValidationTest : WebTest
    {
        public NetFoundationBrandingValidationTest()
        {
            PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            //send a request to home page and check for default home page text.
            var pageRequest = new WebTestRequest(UrlHelper.BaseUrl);
            var homePageTextValidationRuleLogo = AssertAndValidationHelper.GetValidationRuleForFindText(@"<a title="".NET Foundation"" href=""https://www.dotnetfoundation.org""><img src=""/Content/Logos/dnf.png"" alt="".NET Foundation"" /></a>");
            var homePageTextValidationRuleCopyright = AssertAndValidationHelper.GetValidationRuleForFindText(@"&copy; " + DateTime.UtcNow.Year + " .NET Foundation");
            var homePageTextValidationRuleTOU = AssertAndValidationHelper.GetValidationRuleForFindText(@"<a href=""/policies/Terms"">Terms of Use</a>");
            var homePageTextValidationRulePrivacy = AssertAndValidationHelper.GetValidationRuleForFindText(@"<a href=""/policies/Privacy"">Privacy Policy</a>");

            pageRequest.ValidateResponse += homePageTextValidationRuleLogo.Validate;
            pageRequest.ValidateResponse += homePageTextValidationRuleCopyright.Validate;
            pageRequest.ValidateResponse += homePageTextValidationRuleTOU.Validate;
            pageRequest.ValidateResponse += homePageTextValidationRulePrivacy.Validate;
            yield return pageRequest;
        }
    }
}

