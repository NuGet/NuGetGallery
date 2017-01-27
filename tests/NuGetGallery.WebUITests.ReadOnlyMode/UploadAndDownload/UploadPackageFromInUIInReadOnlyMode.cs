// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.WebTesting;
using NuGetGallery.FunctionalTests.Helpers;

namespace NuGetGallery.FunctionalTests.WebUITests.ReadOnlyMode
{
    /// <summary>
    /// Uploads a new test package using Gallery UI in read-only mode and checks if proper error is shown.
    /// </summary>
    public class UploadPackageFromUIInReadOnlyMode : WebTest
    {
        public UploadPackageFromUIInReadOnlyMode()
        {
            PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            var defaultExtractionRule = AssertAndValidationHelper.GetDefaultExtractHiddenFields();

            // Do initial login
            var logonGet = AssertAndValidationHelper.GetLogonGetRequest();
            yield return logonGet;

            var logonPost = AssertAndValidationHelper.GetLogonPostRequest(this);
            yield return logonPost;

            var uploadRequest = AssertAndValidationHelper.GetHttpRequestForUrl(UrlHelper.UploadPageUrl);
            yield return uploadRequest;

            if (LastResponse.ResponseUri.ToString().Contains("verify-upload"))
            {
                // if there is a upload in progress, try to submit that upload instead of creating a new package (since we are just going to verify that upload doesn't go through).
                //Extract the package Id of the pending upload.
                var response = LastResponse.BodyString;
                var startIndex = response.IndexOf("<p>", StringComparison.Ordinal);
                var endIndex = response.IndexOf("</p>", startIndex, StringComparison.Ordinal);
                var packageId = response.Substring(startIndex + 3, endIndex - (startIndex + 3));
                AddCommentToResult(packageId);   //Adding the package ID to result for debugging.
                var verifyUploadPostRequest = AssertAndValidationHelper.GetVerifyPackagePostRequestForPackage(this, packageId, "1.0.0", UrlHelper.VerifyUploadPageUrl, Constants.ReadOnlyModeError, 503);
                yield return verifyUploadPostRequest;
            }
            else
            {
                // The API key is part of the nuget.config file that is present under the solution dir.
                var packageId = DateTime.Now.Ticks.ToString();
                var packageCreationHelper = new PackageCreationHelper();
                var packageFullPath = packageCreationHelper.CreatePackage(packageId).Result;

                var uploadPostRequest = AssertAndValidationHelper.GetUploadPostRequestForPackage(this, packageFullPath);
                yield return uploadPostRequest;

                var verifyUploadRequest = new WebTestRequest(UrlHelper.VerifyUploadPageUrl);
                verifyUploadRequest.ExtractValues += defaultExtractionRule.Extract;
                yield return verifyUploadRequest;

                var verifyUploadPostRequest = AssertAndValidationHelper.GetVerifyPackagePostRequestForPackage(this, packageId, "1.0.0", UrlHelper.VerifyUploadPageUrl, Constants.ReadOnlyModeError, 503);
                yield return verifyUploadPostRequest;
            }
        }
    }
}
