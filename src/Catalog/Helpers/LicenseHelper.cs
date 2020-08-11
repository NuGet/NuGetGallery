// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog
{
    public static class LicenseHelper
    {
        /// <summary>
        /// Generate the license url given package info and gallery base url.
        /// </summary>
        /// <param name="packageId">package Id</param>
        /// <param name="packageVersion">package version</param>
        /// <param name="galleryBaseAddress">url of gallery base address</param>
        /// <returns>The url of license in gallery</returns>
        public static string GetGalleryLicenseUrl(string packageId, string packageVersion, Uri galleryBaseAddress)
        {
            if (galleryBaseAddress == null || string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(packageVersion))
            {
                return null;
            }

            var uriBuilder = new UriBuilder(galleryBaseAddress);
            uriBuilder.Path = string.Join("/", new string[] { "packages", packageId, packageVersion, "license" });

            return uriBuilder.Uri.AbsoluteUri;
        }
    }
}
