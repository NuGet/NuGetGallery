// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace BasicSearchTests.FunctionalTests.Core.Models
{
    public class V2SearchResultEntry
    {
        public V2PackageRegistration PackageRegistration { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string Version { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string NormalizedVersion { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string Summary { get; set; }

        public string Authors { get; set; }

        public string Copyright { get; set; }

        public string Language { get; set; }

        public string Tags { get; set; }

        public string ReleaseNotes { get; set; }

        public string ProjectUrl { get; set; }

        public string IconUrl { get; set; }

        public bool IsLatestStable { get; set; }

        public bool IsLatest { get; set; }

        public bool Listed { get; set; }

        public DateTime Created { get; set; }

        public DateTime Published { get; set; }

        public DateTime LastUpdated { get; set; }

        public DateTime? LastEdited { get; set; }

        public long DownloadCount { get; set; }

        public string FlattenedDependencies { get; set; }

        public object[] Dependencies { get; set; }

        public string[] SupportedFrameworks { get; set; }

        public string[] Frameworks { get; set; }

        public string[] Tfms { get; set; }

        public string Hash { get; set; }

        public string HashAlgorithm { get; set; }

        public long PackageFileSize { get; set; }

        public string LicenseUrl { get; set; }

        public bool RequireslicenseAcceptance { get; set; }
    }
}
