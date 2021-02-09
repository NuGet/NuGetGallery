// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace VerifyGitHubVulnerabilities.Configuration
{
    public class VerifyGitHubVulnerabilitiesConfiguration
    {
        /// <summary>
        /// GitHub's v4 GraphQL API endpoint.
        /// </summary>
        public Uri GitHubGraphQLQueryEndpoint { get; set; } = new Uri("https://api.github.com/graphql");

        /// <summary>
        /// The personal access token to use to authenticate with GitHub.
        /// </summary>
        public string GitHubPersonalAccessToken { get; set; }

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
    }
}