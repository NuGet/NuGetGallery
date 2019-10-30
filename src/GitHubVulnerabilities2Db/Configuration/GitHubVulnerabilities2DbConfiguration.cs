// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace GitHubVulnerabilities2Db.Configuration
{
    public class GitHubVulnerabilities2DbConfiguration
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
        /// The storage connection to use to save the job's cursor.
        /// </summary>
        public string StorageConnectionString { get; set; }

        /// <summary>
        /// The storage container to save the job's cursor in.
        /// </summary>
        public string CursorContainerName { get; set; } = "vulnerability";

        /// <summary>
        /// The name of the blob to save the job's advisories cursor in.
        /// </summary>
        public string AdvisoryCursorBlobName { get; set; } = "cursor.json";
    }
}