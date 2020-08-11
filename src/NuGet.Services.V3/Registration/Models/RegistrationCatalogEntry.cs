// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NuGet.Protocol.Catalog;

namespace NuGet.Protocol.Registration
{
    /// <summary>
    /// Source: https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource#catalog-entry
    /// </summary>
    public class RegistrationCatalogEntry
    {
        [JsonProperty("@id")]
        public string Url { get; set; }

        [JsonProperty("@type")]
        public string Type { get; set; }

        [JsonProperty("authors")]
        public string Authors { get; set; }

        [JsonProperty("dependencyGroups")]
        public List<RegistrationPackageDependencyGroup> DependencyGroups { get; set; }

        [JsonProperty("deprecation")]
        public PackageDeprecation Deprecation { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("iconUrl")]
        public string IconUrl { get; set; }

        [JsonProperty("id")]
        public string PackageId { get; set; }

        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("licenseExpression")]
        public string LicenseExpression { get; set; }

        [JsonProperty("licenseUrl")]
        public string LicenseUrl { get; set; }

        [JsonProperty("listed")]
        public bool Listed { get; set; }

        [JsonProperty("minClientVersion")]
        public string MinClientVersion { get; set; }

        [JsonProperty("packageContent")]
        public string PackageContent { get; set; }

        [JsonProperty("projectUrl")]
        public string ProjectUrl { get; set; }

        [JsonProperty("published")]
        public DateTimeOffset? Published { get; set; }

        [JsonProperty("requireLicenseAcceptance")]
        public bool RequireLicenseAcceptance { get; set; }

        [JsonProperty("summary")]
        public string Summary { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("vulnerabilities")]
        public List<RegistrationPackageVulnerability> Vulnerabilities { get; set; }
    }
}
