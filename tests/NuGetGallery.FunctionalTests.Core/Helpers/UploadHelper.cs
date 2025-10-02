// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
        /// Uploads a package using HttpClient
        /// </summary>
        public static async Task UploadAndVerifyPackageAsync(HttpClient client, PackageToUpload package)
        {
            // Get upload page to extract anti-forgery token
            var uploadPageResponse = await client.GetAsync(UrlHelper.UploadPageUrl);
            uploadPageResponse.EnsureSuccessStatusCode();
            var uploadPageContent = await uploadPageResponse.Content.ReadAsStringAsync();

            // Extract token
            var tokenMatch = Regex.Match(uploadPageContent,
                @"<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)"" />");

            if (!tokenMatch.Success)
            {
                throw new InvalidOperationException("Could not extract anti-forgery token");
            }

            var token = tokenMatch.Groups[1].Value;

            // Create package file
            var packageCreationHelper = new PackageCreationHelper();
            string packagePath = await packageCreationHelper.CreatePackage(package.Id, package.Version);

            // Upload package
            using var packageContent = new MultipartFormDataContent
            {
                { new StringContent(token), "__RequestVerificationToken" }
            };

            using var fileStream = File.OpenRead(packagePath);
            using var fileContent = new StreamContent(fileStream);
            packageContent.Add(fileContent, "UploadFile", Path.GetFileName(packagePath));

            var uploadResponse = await client.PostAsync(UrlHelper.UploadPageUrl, packageContent);
            uploadResponse.EnsureSuccessStatusCode();

            // Verify package
            var verifyContent = new MultipartFormDataContent
            {
                { new StringContent(token), "__RequestVerificationToken" },
                { new StringContent(package.Id), "Id" },
                { new StringContent(package.Version), "Version" },
                { new StringContent(""), "LicenseUrl" },
                { new StringContent(""), "Edit.VersionTitle" },
                { new StringContent("Package description"), "Edit.Description" },
                { new StringContent(""), "Edit.Summary" },
                { new StringContent(""), "Edit.IconUrl" },
                { new StringContent(""), "Edit.ProjectUrl" },
                { new StringContent("nugettest"), "Edit.Authors" },
                { new StringContent("Copyright 2013"), "Edit.CopyrightText" },
                { new StringContent("windows8"), "Edit.Tags" },
                { new StringContent(""), "Edit.ReleaseNotes" }
            };

            if (!string.IsNullOrEmpty(package.Owner))
            {
                verifyContent.Add(new StringContent(package.Owner), "Owner");
            }

            var verifyResponse = await client.PostAsync(UrlHelper.VerifyUploadPageUrl, verifyContent);
            verifyResponse.EnsureSuccessStatusCode();
        }
    }
}
