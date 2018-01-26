// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.WebTesting;
using System.Linq;
using System;

namespace NuGetGallery.FunctionalTests.Helpers
{
    public static class UploadHelper
    {
        /// <summary>
        /// Helper class for defining the properties of a test package to be uploaded.
        /// </summary>
        public class PackageToUpload
        {
            /// <summary>
            /// The ID of the package to upload.
            /// </summary>
            public string Id { get; }

            /// <summary>
            /// The version of the package to upload.
            /// </summary>
            public string Version { get; }
            
            /// <summary>
            /// The username of the user that will be specified as the owner of the package in the verification form.
            /// </summary>
            public string Owner { get; }

            public PackageToUpload(string id = null, string version = null, string owner = null)
            {
                Owner = owner ?? EnvironmentSettings.TestAccountName;
                Id = id ?? GetUniquePackageId(Owner);
                Version = version ?? GetUniquePackageVersion();
            }

            protected PackageToUpload(PackageToUpload package)
            {
                Id = package.Id;
                Version = package.Version;
                Owner = package.Owner;
            }
        }

        /// <summary>
        /// Gets a unique ID for a package to upload.
        /// </summary>
        public static string GetUniquePackageId(string owner)
        {
            return $"UploadPackageFromUI.{owner}.{DateTimeOffset.UtcNow.Ticks}";
        }

        /// <summary>
        /// Gets a unique version for a package to upload.
        /// </summary>
        public static string GetUniquePackageVersion()
        {
            var ticks = DateTimeOffset.UtcNow.Ticks;
            return $"{(ticks / 1000000) % 100}.{(ticks / 10000) % 100}.{(ticks / 100) % 100}-at{ticks}";
        }

        /// <summary>
        /// Uploads a new test package using Gallery UI. Validates that logon prompt appears to upload and checks that the package's home page opens post upload.
        /// </summary>
        public static IEnumerator<WebTestRequest> UploadPackage(WebTest test, string owner)
        {
            return UploadPackages(test, new PackageToUpload(owner: owner));
        }

        /// <summary>
        /// Uploads a set of test packages using Gallery UI. Validates that logon prompt appears to upload and checks that the package's home page opens post upload.
        /// </summary>
        public static IEnumerator<WebTestRequest> UploadPackages(WebTest test, params PackageToUpload[] packages)
        {
            return UploadPackages(test, packages.Select(p => new PackageToUploadInternal(p)));
        }

        public class PackageToUploadInternal : PackageToUpload
        {
            public string FullPath { get; }

            public PackageToUploadInternal(PackageToUpload package)
                : base(package)
            {
                FullPath = new PackageCreationHelper().CreatePackage(Id, Version).Result;
            }
        }

        private static IEnumerator<WebTestRequest> UploadPackages(WebTest test, IEnumerable<PackageToUploadInternal> packagesToUpload)
        {
            return 
                Login(test)
                    .Concat(packagesToUpload.SelectMany(p => UploadPackageAfterLogin(test, p)))
                    .GetEnumerator();
        }
        
        private static IEnumerable<WebTestRequest> Login(WebTest test)
        {
            var defaultExtractionRule = AssertAndValidationHelper.GetDefaultExtractHiddenFields();

            // Do initial login
            var logonGet = AssertAndValidationHelper.GetLogonGetRequest();
            yield return logonGet;

            var logonPost = AssertAndValidationHelper.GetLogonPostRequest(test);
            yield return logonPost;
        }

        private static IEnumerable<WebTestRequest> UploadPackageAfterLogin(WebTest test, PackageToUploadInternal packageToUpload)
        {
            var uploadRequest = AssertAndValidationHelper.GetHttpRequestForUrl(UrlHelper.UploadPageUrl);
            yield return uploadRequest;

            var uploadPostRequest = AssertAndValidationHelper.GetUploadPostRequestForPackage(test, packageToUpload.FullPath);
            yield return uploadPostRequest;

            // This second get request to upload is to put us on the new "Verify Page" which is just the upload page in a different state.
            // This is to get the RequestVerificationToken for the following request. (upload and verify were merged onto the same page).
            var uploadRequest2 = AssertAndValidationHelper.GetHttpRequestForUrl(UrlHelper.UploadPageUrl);
            yield return uploadRequest2;

            var verifyUploadPostRequest = AssertAndValidationHelper.GetVerifyPackagePostRequestForPackage(test,
                packageToUpload.Id,
                packageToUpload.Version,
                UrlHelper.GetPackagePageUrl(packageToUpload.Id, packageToUpload.Version),
                packageToUpload.Id,
                packageToUpload.Owner);
            yield return verifyUploadPostRequest;
        }
    }
}
