// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NuGet.Protocol;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Monitoring.Model;
using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// The metadata for a particular package in its registration index.
    /// </summary>
    public class PackageRegistrationIndexMetadata : PackageRegistrationLeafMetadata
    {
        public string Id { get; set; }

        [JsonConverter(typeof(NuGetVersionConverter))]
        public NuGetVersion Version { get; set; }

        /// <remarks>
        /// In the database, this property is called "RequiresLicenseAcceptance" (notice the "s").
        /// </remarks>
        public bool RequireLicenseAcceptance { get; set; }

        public PackageRegistrationDeprecationMetadata Deprecation { get; set; }

        /// <summary>
        /// Default constructor for JSON serialization purposes.
        /// </summary>
        public PackageRegistrationIndexMetadata()
        {
        }

        /// <summary>
        /// Converts a <see cref="FeedPackageDetails"/> into a format that can be directly compared to a <see cref="PackageRegistrationIndexMetadata"/>.
        /// </summary>
        public PackageRegistrationIndexMetadata(FeedPackageDetails package)
            : base(package)
        {
            Id = package.PackageId;
            Version = NuGetVersion.Parse(package.PackageVersion);
            RequireLicenseAcceptance = package.RequiresLicenseAcceptance;
            if (package.HasDeprecationInfo)
            {
                Deprecation = new PackageRegistrationDeprecationMetadata(package.DeprecationInfo);
            }
        }
    }
}
