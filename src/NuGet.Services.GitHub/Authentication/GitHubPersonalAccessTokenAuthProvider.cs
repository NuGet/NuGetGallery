// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using NuGet.Services.GitHub.Configuration;

namespace NuGet.Services.GitHub.Authentication
{
    public class GitHubPersonalAccessTokenAuthProvider : IGitHubAuthProvider
    {
        private readonly string _personalAccessToken;

        public GitHubPersonalAccessTokenAuthProvider(GraphQLQueryConfiguration configuration)
        {
            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            _personalAccessToken = configuration.GitHubPersonalAccessToken ?? throw new ArgumentException("GitHub personal access token must be provided in the configuration.", nameof(configuration));
        }

        public Task AddAuthentication(HttpRequestMessage message)
        {
            message.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", _personalAccessToken);
            return Task.CompletedTask;
        }
    }
}
