// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.GitHub.Configuration;

namespace VerifyGitHubVulnerabilities.Configuration
{
    public class VerifyGitHubVulnerabilitiesConfiguration : GraphQLQueryConfiguration
    {
        /// <summary>
        /// The v3 index URI string for fetching registration metadata
        /// </summary>
        public string NuGetV3Index { get; set; }

        /// <summary>
        /// Whether to verify GitHubVulnerabilities in the gallery database
        /// </summary>
        public bool VerifyDatabase { get; set; } = true;

        /// <summary>
        /// Whether to verify GitHubVulnerabilities in the registration blobs
        /// </summary>
        public bool VerifyRegistrationMetadata { get; set; } = true;

        /// <summary>
        /// The User-Agent header to send with each request to GitHub.
        /// </summary>
        public override string UserAgent { get; set; } = "NuGet.VerifyGitHubVulnerabilities";
    }
}
