// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Services.GitHub.Configuration;

namespace NuGet.Services.GitHub.GraphQL
{
    public class QueryService : IQueryService
    {
        private readonly GraphQLQueryConfiguration _configuration;
        private readonly HttpClient _client;
        private readonly ILogger<QueryService> _logger;

        public QueryService(
            GraphQLQueryConfiguration configuration,
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
            var queryResponse = JsonConvert.DeserializeObject<QueryResponse>(response);

            if (queryResponse.Errors != null && queryResponse.Errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "The GitHub GraphQL response returned errors in the response JSON. " +
                    $"Response body:{Environment.NewLine}{response}");
            }

            return queryResponse;
        }

        private async Task<string> MakeWebRequestAsync(string query, CancellationToken token)
        {
            var attempt = 0;
            const int maxAttempts = 5;
            HttpStatusCode? lastStatusCode = null;
            while (true)
            {
                try
                {
                    attempt++;
                    lastStatusCode = null;
                    using (var request = CreateRequest(query))
                    using (var response = await _client.SendAsync(request, token))
                    {
                        lastStatusCode = response.StatusCode;
                        response.EnsureSuccessStatusCode();
                        return await response.Content.ReadAsStringAsync();
                    }
                }
                catch (Exception ex) when (
                    !token.IsCancellationRequested
                    && attempt < maxAttempts
                    && (!lastStatusCode.HasValue || lastStatusCode >= HttpStatusCode.InternalServerError)) // do not retry for 4XX errors
                {
                    var delay = _configuration.RetryDelay;
                    _logger.LogWarning(ex, "Failed attempt {Attempt} to query GitHub GraphQL API. Last HTTP status code: {Status}. Waiting {Seconds} seconds.", attempt, lastStatusCode, delay.TotalSeconds);
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
            message.Headers.UserAgent.TryParseAdd(_configuration.UserAgent);
            return message;
        }
    }
}
