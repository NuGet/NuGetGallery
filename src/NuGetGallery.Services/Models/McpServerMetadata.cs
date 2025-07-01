// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGetGallery.Services.Models
{
    public class McpServerMetadata
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("version_detail")]
        public VersionDetail VersionDetail { get; set; }

        [JsonProperty("packages")]
        public List<McpPackage> Packages { get; set; }

        [JsonProperty("repository")]
        public Repository Repository { get; set; }
    }

    public class VersionDetail
    {
        [JsonProperty("version")]
        public string Version { get; set; }
    }

    public class McpPackage
    {
        [JsonProperty("registry_name")]
        public string RegistryName { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("package_arguments")]
        public List<PackageArgument> PackageArguments { get; set; }

        [JsonProperty("environment_variables")]
        public List<EnvironmentVariable> EnvironmentVariables { get; set; }
    }

    public class PackageArgument
    {
        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("is_required")]
        public bool IsRequired { get; set; }

        [JsonProperty("format")]
        public string Format { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("default")]
        public string Default { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("value_hint")]
        public string ValueHint { get; set; }
    }

    public class EnvironmentVariable
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }
    }

    public class Repository
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; }
    }
}
