// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.WebTesting;
using NuGetGallery.FunctionalTests.Helpers;

namespace NuGetGallery.FunctionalTests.WebUITests.BasicPages
{
    /// <summary>
    /// Sends http request to gallery home page checks for the configured branding text in the reponse.
    /// priority : p1
    /// </summary>
    public class BrandingValidationTest : WebTest
    {
        public BrandingValidationTest()
        {
            PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            //send a request to home page and check for default home page text.
            var pageRequest = new WebTestRequest(UrlHelper.BaseUrl);
            
            if (String.IsNullOrEmpty(EnvironmentSettings.ExternalBrandingMessage)
                && String.IsNullOrEmpty(EnvironmentSettings.ExternalBrandingUrl)
                && String.IsNullOrEmpty(EnvironmentSettings.ExternalAboutUrl)
                && String.IsNullOrEmpty(EnvironmentSettings.ExternalPrivacyPolicyUrl)
                && String.IsNullOrEmpty(EnvironmentSettings.ExternalTermsOfUseUrl)
                && String.IsNullOrEmpty(EnvironmentSettings.ExternalTrademarksUrl))
            {
                var homePageTextValidationRuleLogo = AssertAndValidationHelper.GetValidationRuleForFindText(@"<a href=""https://www.dotnetfoundation.org"">");
                var homePageTextValidationRuleCopyright = AssertAndValidationHelper.GetValidationRuleForFindText(@"&copy; " + DateTime.UtcNow.Year + " .NET Foundation");
                var homePageTextValidationRuleTOU = AssertAndValidationHelper.GetValidationRuleForFindText($@"<a href=""{UrlHelper.BaseUrl}policies/Terms"">Terms of Use</a>");
                var homePageTextValidationRulePrivacy = AssertAndValidationHelper.GetValidationRuleForFindText($@"<a href=""{UrlHelper.BaseUrl}policies/Privacy"">Privacy Policy</a>");

                pageRequest.ValidateResponse += homePageTextValidationRuleLogo.Validate;
                pageRequest.ValidateResponse += homePageTextValidationRuleCopyright.Validate;
                pageRequest.ValidateResponse += homePageTextValidationRuleTOU.Validate;
                pageRequest.ValidateResponse += homePageTextValidationRulePrivacy.Validate;
            }
            else
            {
                if (!String.IsNullOrEmpty(EnvironmentSettings.ExternalBrandingMessage))
                {
                    var validationBrandingMessage = AssertAndValidationHelper.GetValidationRuleForFindText(string.Format(EnvironmentSettings.ExternalBrandingMessage, DateTime.UtcNow.Year));
                    pageRequest.ValidateResponse += validationBrandingMessage.Validate;
                }

                if (!String.IsNullOrEmpty(EnvironmentSettings.ExternalBrandingUrl))
                {
                    var validationBrandingUrl = AssertAndValidationHelper.GetValidationRuleForFindText($@"<a href=""{EnvironmentSettings.ExternalBrandingUrl}"">");
                    pageRequest.ValidateResponse += validationBrandingUrl.Validate;
                }

                if (!String.IsNullOrEmpty(EnvironmentSettings.ExternalAboutUrl))
                {
                    var validationAboutUrl = AssertAndValidationHelper.GetValidationRuleForFindText($@"<a href=""{EnvironmentSettings.ExternalAboutUrl}"">");
                    pageRequest.ValidateResponse += validationAboutUrl.Validate;
                }

                if (!String.IsNullOrEmpty(EnvironmentSettings.ExternalPrivacyPolicyUrl))
                {
                    var validationPrivacyPolicyUrl = AssertAndValidationHelper.GetValidationRuleForFindText($@"<a href=""{EnvironmentSettings.ExternalPrivacyPolicyUrl}"">");
                    pageRequest.ValidateResponse += validationPrivacyPolicyUrl.Validate;
                }

                if (!String.IsNullOrEmpty(EnvironmentSettings.ExternalTermsOfUseUrl))
                {
                    var validationTermsOfUseUrl = AssertAndValidationHelper.GetValidationRuleForFindText($@"<a href=""{EnvironmentSettings.ExternalTermsOfUseUrl}"">");
                    pageRequest.ValidateResponse += validationTermsOfUseUrl.Validate;
                }

                if (!String.IsNullOrEmpty(EnvironmentSettings.ExternalTrademarksUrl))
                {
                    var validationTrademarksUrl = AssertAndValidationHelper.GetValidationRuleForFindText($@"<a href=""{EnvironmentSettings.ExternalTrademarksUrl}"">");
                    pageRequest.ValidateResponse += validationTrademarksUrl.Validate;
                }
            }

            yield return pageRequest;
        }
    }
}