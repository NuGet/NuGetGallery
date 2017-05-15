// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NuGet.Protocol;
using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// The metadata for a particular package in its registration index.
    /// </summary>
    public class PackageRegistrationIndexMetadata : PackageRegistrationLeafMetadata
    {
        public string Authors { get; set; }

        public string Description { get; set; }

        public string IconUrl { get; set; }

        public string Id { get; set; }

        public string LicenseUrl { get; set; }

        [JsonConverter(typeof(NullableNuGetVersionConverter))]
        public NuGetVersion MinClientVersion { get; set; }

        public string ProjectUrl { get; set; }

        public bool RequireLicenseAcceptance { get; set; }

        public string Summary { get; set; }

        public IEnumerable<string> Tags { get; set; }

        public string Title { get; set; }

        [JsonConverter(typeof(NuGetVersionConverter))]
        public NuGetVersion Version { get; set; }

        /// <summary>
        /// Default constructor for JSON serialization purposes.
        /// </summary>
        public PackageRegistrationIndexMetadata()
        {
        }

        /// <summary>
        /// Converts a <see cref="V2FeedPackageInfo"/> into a format that can be directly compared to a <see cref="PackageRegistrationIndexMetadata"/>.
        /// </summary>
        public PackageRegistrationIndexMetadata(V2FeedPackageInfo package)
            : base(package)
        {
            Authors = string.Join(", ", package.Authors);
            Description = package.Description;
            IconUrl = package.IconUrl;
            Id = package.Id;
            MinClientVersion = package.MinClientVersion;
            ProjectUrl = package.ProjectUrl;
            RequireLicenseAcceptance = package.RequireLicenseAcceptance;
            Summary = package.Summary;
            Tags = package.Tags.Split(' ').Where(s => !string.IsNullOrEmpty(s)).ToArray();
            Title = package.Title;
            Version = package.Version;
        }
    }
}
