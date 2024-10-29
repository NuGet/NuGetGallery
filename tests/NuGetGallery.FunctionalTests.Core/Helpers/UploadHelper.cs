// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.WebTesting;

namespace NuGetGallery.FunctionalTests.Helpers
{
    public static class UploadHelper
    {
        private static readonly object UniqueLock = new object();

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

            public PackageToUpload(string id, string version = null, string owner = null)
            {
                Id = id;
                Version = version ?? GetUniquePackageVersion();
                Owner = owner ?? GalleryConfiguration.Instance.Account.Name;
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
        public static string GetUniquePackageId()
        {
            lock (UniqueLock)
            {
                return $"NuGetFunctionalTest_{Guid.NewGuid():N}";
            }
        }

        /// <summary>
        /// Gets a unique version for a package to upload.
        /// </summary>
        public static string GetUniquePackageVersion()
        {
            lock (UniqueLock)
            {
                var ticks = DateTimeOffset.UtcNow.Ticks;
                return $"1.0.0-v{ticks}";
            }
        }

        /// <summary>
        /// Uploads a set of test packages using Gallery UI. Validates that logon prompt appears to upload and checks that the package's home page opens post upload.
        /// </summary>
        public static IEnumerator<WebTestRequest> UploadPackages(WebTest test, IEnumerable<PackageToUpload> packages)
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
                    .Concat(packagesToUpload.SelectMany(p => UploadPackage(test, p)))
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

        private static IEnumerable<WebTestRequest> UploadPackage(WebTest test, PackageToUploadInternal packageToUpload)
        {
            // Navigate to the upload page.
            var uploadRequest = AssertAndValidationHelper.GetHttpRequestForUrl(UrlHelper.UploadPageUrl);
            yield return uploadRequest;

            // Cancel any pending uploads.
            // We can't upload the new package if any uploads are pending.
            var cancelUploadPostRequest = AssertAndValidationHelper.GetCancelUploadPostRequestForPackage(test);
            yield return cancelUploadPostRequest;

            // Upload the new package.
            var uploadPostRequest = AssertAndValidationHelper.GetUploadPostRequestForPackage(test, packageToUpload.FullPath);
            yield return uploadPostRequest;

            // Verify the new package.
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
