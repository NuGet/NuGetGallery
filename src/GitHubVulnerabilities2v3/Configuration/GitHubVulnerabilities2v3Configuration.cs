// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.GitHub.Configuration;

namespace GitHubVulnerabilities2v3.Configuration
{
    public class GitHubVulnerabilities2v3Configuration : GraphQLQueryConfiguration
    {
        /// <summary>
        /// The storage connection to use to save the job's output.
        /// </summary>
        public string StorageConnectionString { get; set; }

        /// <summary>
        /// The storage container to save the job's output in.
        /// </summary>
        public string V3VulnerabilityContainerName { get; set; } = "v3-vulnerabilities";

        /// <summary>
        /// Service Index Root
        /// </summary>
        public string V3IndexUrl { get; set; } = "https://api.nuget.org/";

        /// <summary>
        /// The name of the blob to save the job's advisories cursor in.
        /// </summary>
        public string AdvisoryCursorBlobName { get; set; } = "cursor.json";

        /// <summary>
        /// The names of the generated files.
        /// </summary>
        public string IndexFileName { get; set; } = "index.json";
        public string BaseFileName { get; set; } = "vulnerability.base.json";
        public string UpdateFileName { get; set; } = "vulnerability.update.json";
    }
}