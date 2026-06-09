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
        public string GitHubPersonalAccessToken { get; set; } = null;

        /// <summary>
        /// If using GitHub App authentication, the GitHub App Client Id to use to authenticate with GitHub.
        /// </summary>
        /// <remarks>
        /// If specified, the <see cref="GitHubPersonalAccessToken"/> will be ignored and GitHub App authentication will be used instead of personal access token authentication.
        /// If specified, the <see cref="GitHubAppPrivateKeyName"/> and <see cref="GitHubInstallationName"/> must also be specified.
        /// </remarks>
        public string GitHubAppId { get; set; } = null;

        /// <summary>
        /// GitHub App installation name, will determine the auth scope of generated tokens.
        /// </summary>
        public string GitHubInstallationName { get; set; } = null;

        /// <summary>
        /// The name of the private key used to sign GitHub App auth tokens
        /// </summary>
        public string GitHubAppPrivateKeyName { get; set; } = null;

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
