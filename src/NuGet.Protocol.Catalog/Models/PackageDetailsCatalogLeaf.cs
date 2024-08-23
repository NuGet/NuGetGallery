﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Protocol.Catalog
{
    public class PackageDetailsCatalogLeaf : CatalogLeaf
    {
        [JsonProperty("authors")]
        public string Authors { get; set; }

        [JsonProperty("copyright")]
        public string Copyright { get; set; }

        [JsonProperty("created")]
        public DateTimeOffset Created { get; set; }

        [JsonProperty("lastEdited")]
        public DateTimeOffset LastEdited { get; set; }

        [JsonProperty("dependencyGroups")]
        public List<PackageDependencyGroup> DependencyGroups { get; set; }

        [JsonProperty("deprecation")]
        public PackageDeprecation Deprecation { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("iconUrl")]
        public string IconUrl { get; set; }

        /// <summary>
        /// Note that an old bug in the NuGet.org catalog had this wrong in some cases.
        /// Example: https://api.nuget.org/v3/catalog0/data/2016.03.11.21.02.55/mvid.fody.2.json
        /// </summary>
        [JsonProperty("isPrerelease")]
        public bool IsPrerelease { get; set; }

        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("licenseUrl")]
        public string LicenseUrl { get; set; }

        [JsonProperty("listed")]
        public bool? Listed { get; set; }

        [JsonProperty("minClientVersion")]
        public string MinClientVersion { get; set; }

        [JsonProperty("packageEntries")]
        public List<PackageEntry> PackageEntries { get; set; }

        [JsonProperty("packageHash")]
        public string PackageHash { get; set; }

        [JsonProperty("packageHashAlgorithm")]
        public string PackageHashAlgorithm { get; set; }

        [JsonProperty("packageSize")]
        public long PackageSize { get; set; }

        [JsonProperty("packageTypes")]
        public List<PackageType> PackageTypes { get; set; }

        [JsonProperty("projectUrl")]
        public string ProjectUrl { get; set; }

        [JsonProperty("releaseNotes")]
        public string ReleaseNotes { get; set; }

        [JsonProperty("requireLicenseAcceptance")]
        public bool? RequireLicenseAcceptance { get; set; }

        [JsonProperty("summary")]
        public string Summary { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("verbatimVersion")]
        public string VerbatimVersion { get; set; }

        [JsonProperty("licenseExpression")]
        public string LicenseExpression { get; set; }

        [JsonProperty("licenseFile")]
        public string LicenseFile { get; set; }

        [JsonProperty("iconFile")]
        public string IconFile { get; set; }

        [JsonProperty("readmeFile")]
        public string ReadmeFile { get; set; }

        [JsonProperty("vulnerabilities")]
        public List<PackageVulnerability> Vulnerabilities { get; set; }
    }
}
