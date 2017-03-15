// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace BasicSearchTests.FunctionalTests.Core.Models
{
    public class SearchResultEntry
    {
        [JsonProperty("@id")]
        public string AtId;

        [JsonProperty("@type")]
        public string AtType;

        public string Registration;

        public string Id;

        public string Version;

        public string Description;

        public string Summary;

        public string Title;

        public string IconUrl;

        public string LicenseUrl;

        public string ProjectUrl;

        public string[] Tags;

        public string[] Authors;

        public long TotalDownloads;

        public PackageVersion[] Versions;

    }
}
