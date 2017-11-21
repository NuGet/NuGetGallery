// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.WebTesting;
using NuGetGallery.FunctionalTests.Helpers;

namespace NuGetGallery.FunctionalTests.WebUITests.UploadAndDownload
{
    /// <summary>
    /// Uploads a new test package using Gallery UI. Validates that logon prompt appears to upload and checks that the package's home page opens post upload.
    /// priority : p0
    /// </summary>
    public class UploadPackageFromUI : WebTest
    {
        public UploadPackageFromUI()
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
            
            var packageId = $"UploadPackageFromUI.{DateTimeOffset.UtcNow.Ticks}";
            var packageCreationHelper = new PackageCreationHelper();
            var packageFullPath = packageCreationHelper.CreatePackage(packageId).Result;
            var uploadPostRequest = AssertAndValidationHelper.GetUploadPostRequestForPackage(this, packageFullPath);
            yield return uploadPostRequest;

            //This second get request to upload is to put us on the new "Verify Page"
            // which is just the upload page in a different state.
            // This is to get the RequestVerificationToken for the folloing request. (upload and verify were merged onto the same page).
            var uploadRequest2 = AssertAndValidationHelper.GetHttpRequestForUrl(UrlHelper.UploadPageUrl);
            yield return uploadRequest2;

            var verifyUploadPostRequest = AssertAndValidationHelper.GetVerifyPackagePostRequestForPackage(this, packageId, "1.0.0", UrlHelper.GetPackagePageUrl(packageId, "1.0.0"), packageId);
            yield return verifyUploadPostRequest;
        }
    }
}
