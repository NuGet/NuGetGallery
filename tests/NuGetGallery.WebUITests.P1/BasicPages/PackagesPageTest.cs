// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.WebTesting;
using NuGetGallery.FunctionalTests.Helpers;

namespace NuGetGallery.FunctionalTests.WebUITests.BasicPages
{
    /// <summary>
    /// Sends http request to individual package pages and checks the response for appropriate title and download count.
    /// priority : p1
    /// </summary>
    public class PackagesPageTest
        : WebTest
    {
        public PackagesPageTest()
        {
            PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            // Use a predefined test package.
            var packageId = Constants.TestPackageId;
            var packagePageRequest = new WebTestRequest(UrlHelper.BaseUrl + @"/Packages/" + packageId);

            // Rule to check if the title contains the package id and the latest stable version of the package.
            var packageTitleValidationRule = AssertAndValidationHelper.GetValidationRuleForFindText(packageId + " " + ClientSdkHelper.GetLatestStableVersion(packageId));
            packagePageRequest.ValidateResponse += packageTitleValidationRule.Validate;

            yield return packagePageRequest;
        }
    }
}
