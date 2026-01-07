// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.WebTesting;
using NuGetGallery.FunctionalTests.Helpers;

namespace NuGetGallery.FunctionalTests.WebUITests.BasicPages
{
    /// <summary>
    /// Sends a http request to the statistics page and tries to validate the default stats page text and the prescene of top package.
    /// Priority : p1
    /// </summary>
    public class StatisticsPageTest : WebTest
    {
        public StatisticsPageTest()
        {
            PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            var statsPageRequest = new WebTestRequest(UrlHelper.StatsPageUrl);

            // Checks for the presence of the rank element. This means there is at least one package in the list.
            var validationRule = AssertAndValidationHelper.GetValidationRuleForFindText(
                @"<h2 class=""stats-title-text"">");
            statsPageRequest.ValidateResponse += validationRule.Validate;

            // Validation rule to check for the default text in stats page.
            var statsPageDefaultTextValidationRule = AssertAndValidationHelper.GetValidationRuleForFindText(Constants.StatsPageDefaultText);
            statsPageRequest.ValidateResponse += statsPageDefaultTextValidationRule.Validate;

            yield return statsPageRequest;
        }
    }
}