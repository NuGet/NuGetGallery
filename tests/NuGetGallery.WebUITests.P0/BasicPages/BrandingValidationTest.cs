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
            
            if (String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.Message)
                && String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.Url)
                && String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.AboutUrl)
                && String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.PrivacyPolicyUrl)
                && String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.TermsOfUseUrl)
                && String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.TrademarksUrl))
            {
                var homePageTextValidationRuleLogo = AssertAndValidationHelper.GetValidationRuleForFindText(@"<a href=""https://www.dotnetfoundation.org"">");
                var homePageTextValidationRuleCopyright = AssertAndValidationHelper.GetValidationRuleForFindText(@"&copy; " + DateTime.UtcNow.Year + " .NET Foundation");
                var homePageTextValidationRuleTOU = AssertAndValidationHelper.GetValidationRuleForFindText($@"<a href=""{UrlHelper.BaseUrl}policies/Terms"">Terms of Use</a>");
                var homePageTextValidationRulePrivacy = AssertAndValidationHelper.GetValidationRuleForFindText($@"<a href=""{UrlHelper.BaseUrl}policies/Privacy"" id=""footer-privacy-policy-link"">Privacy Policy</a>");

                pageRequest.ValidateResponse += homePageTextValidationRuleLogo.Validate;
                pageRequest.ValidateResponse += homePageTextValidationRuleCopyright.Validate;
                pageRequest.ValidateResponse += homePageTextValidationRuleTOU.Validate;
                pageRequest.ValidateResponse += homePageTextValidationRulePrivacy.Validate;
            }
            else
            {
                if (!String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.Message))
                {
                    var validationBrandingMessage = AssertAndValidationHelper.GetValidationRuleForFindText(string.Format(GalleryConfiguration.Instance.Branding.Message, DateTime.UtcNow.Year));
                    pageRequest.ValidateResponse += validationBrandingMessage.Validate;
                }

                if (!String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.Url))
                {
                    var validationBrandingUrl = AssertAndValidationHelper.GetValidationRuleForFindText($@"<a href=""{GalleryConfiguration.Instance.Branding.Url}"">");
                    pageRequest.ValidateResponse += validationBrandingUrl.Validate;
                }

                if (!String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.AboutUrl))
                {
                    var validationAboutUrl = AssertAndValidationHelper.GetValidationRuleForFindText($@"<a href=""{GalleryConfiguration.Instance.Branding.AboutUrl}"">");
                    pageRequest.ValidateResponse += validationAboutUrl.Validate;
                }

                if (!String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.PrivacyPolicyUrl))
                {
                    var validationPrivacyPolicyUrl = AssertAndValidationHelper.GetValidationRuleForFindText($@"<a href=""{GalleryConfiguration.Instance.Branding.PrivacyPolicyUrl}"" id=""footer-privacy-policy-link"">");
                    pageRequest.ValidateResponse += validationPrivacyPolicyUrl.Validate;
                }

                if (!String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.TermsOfUseUrl))
                {
                    var validationTermsOfUseUrl = AssertAndValidationHelper.GetValidationRuleForFindText($@"<a href=""{GalleryConfiguration.Instance.Branding.TermsOfUseUrl}"">");
                    pageRequest.ValidateResponse += validationTermsOfUseUrl.Validate;
                }

                if (!String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.TrademarksUrl))
                {
                    var validationTrademarksUrl = AssertAndValidationHelper.GetValidationRuleForFindText($@"<a href=""{GalleryConfiguration.Instance.Branding.TrademarksUrl}"">");
                    pageRequest.ValidateResponse += validationTrademarksUrl.Validate;
                }
            }

            yield return pageRequest;
        }
    }
}