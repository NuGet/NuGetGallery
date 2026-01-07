// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.GitHub.Configuration
{
    public abstract class GraphQLQueryConfiguration
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
        /// The User-Agent header to send with each request to GitHub.
        /// </summary>
        public abstract string UserAgent { get; set; }

        /// <summary>
        /// Time to wait between retries when a request to GitHub fails.
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(3);
    }
}
