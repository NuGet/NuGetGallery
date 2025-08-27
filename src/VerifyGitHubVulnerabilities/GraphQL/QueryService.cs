// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.GitHub.GraphQL;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VerifyGitHubVulnerabilities.Configuration;
using Microsoft.Extensions.Logging;

namespace VerifyGitHubVulnerabilities.GraphQL
{
    public class QueryService : IQueryService
    {
        /// <remarks>
        /// GitHub requires that every request includes a UserAgent.
        /// </remarks>
        public const string UserAgent = "NuGet.Jobs.VerifyGitHubVulnerabilities";

        private readonly VerifyGitHubVulnerabilitiesConfiguration _configuration;
        private readonly HttpClient _client;
        private readonly ILogger<QueryService> _logger;

        public QueryService(
            VerifyGitHubVulnerabilitiesConfiguration configuration,
            HttpClient client,
            ILogger<QueryService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<QueryResponse> QueryAsync(string query, CancellationToken token)
        {
            var queryJObject = new JObject
            {
                ["query"] = query
            };

            var response = await MakeWebRequestAsync(queryJObject.ToString(), token);
            return JsonConvert.DeserializeObject<QueryResponse>(response);
        }

        private async Task<string> MakeWebRequestAsync(string query, CancellationToken token)
        {
            var attempt = 0;
            const int maxAttempts = 5;
            while (true)
            {
                try
                {
                    attempt++;
                    using (var request = CreateRequest(query))
                    using (var response = await _client.SendAsync(request, token))
                    {
                        response.EnsureSuccessStatusCode();
                        return await response.Content.ReadAsStringAsync();
                    }
                }
                catch (Exception ex) when (!token.IsCancellationRequested && attempt < maxAttempts)
                {
                    var delay = TimeSpan.FromSeconds(3);
                    _logger.LogWarning(ex, "Failed attempt {Attempt} to query GitHub GraphQL API. Waiting {Seconds} seconds.", attempt, delay.TotalSeconds);
                    await Task.Delay(delay, token);
                }
            }
        }

        private HttpRequestMessage CreateRequest(string query)
        {
            var message = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = _configuration.GitHubGraphQLQueryEndpoint,
                Content = new StringContent(query, Encoding.UTF8, "application/json")
            };

            message.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", _configuration.GitHubPersonalAccessToken);
            message.Headers.UserAgent.TryParseAdd(UserAgent);
            return message;
        }
    }
}
