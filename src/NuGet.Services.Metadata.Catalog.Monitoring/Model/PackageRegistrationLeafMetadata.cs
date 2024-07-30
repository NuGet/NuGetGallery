// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// The metadata for a particular package in its registration leaf.
    /// See: https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource#registration-leaf
    /// </summary>
    public class PackageRegistrationLeafMetadata
    {
        public bool Listed { get; set; }

        public DateTimeOffset? Published { get; set; }

        /// <summary>
        /// Default constructor for JSON serialization purposes.
        /// </summary>
        public PackageRegistrationLeafMetadata()
        {
        }

        /// <summary>
        /// Converts a <see cref="FeedPackageDetails"/> into a format that can be directly compared to a <see cref="PackageRegistrationLeafMetadata"/>.
        /// </summary>
        public PackageRegistrationLeafMetadata(FeedPackageDetails package)
        {
            Listed = PackageCatalogItem.GetListed(package.PublishedDate);
            Published = package.PublishedDate;
        }
    }
}
