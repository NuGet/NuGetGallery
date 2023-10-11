// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.GitHub.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.GitHub.GraphQL
{
    public class QueryService : IQueryService
    {
        /// <remarks>
        /// GitHub requires that every request includes a UserAgent.
        /// </remarks>
        public const string UserAgent = "NuGet.Services.GitHub";

        private readonly GraphQLQueryConfiguration _configuration;
        private readonly HttpClient _client;

        public QueryService(
            GraphQLQueryConfiguration configuration,
            HttpClient client)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _client = client ?? throw new ArgumentNullException(nameof(client));
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
            using (var request = CreateRequest(query))
            using (var response = await _client.SendAsync(request, token))
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"The GitHub GraphQL response returned status code {(int)response.StatusCode} {response.ReasonPhrase}. " +
                        $"Response body:{Environment.NewLine}{responseBody}");
                }

                return responseBody;
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
