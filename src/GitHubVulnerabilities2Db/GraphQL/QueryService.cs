// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GitHubVulnerabilities2Db.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GitHubVulnerabilities2Db.GraphQL
{
    public class QueryService : IQueryService
    {
        /// <remarks>
        /// GitHub requires that every request includes a UserAgent.
        /// </remarks>
        public const string UserAgent = "NuGet.Jobs.GitHubVulnerabilities2Db";

        public QueryService(
            InitializationConfiguration initializationConfiguration,
            HttpClient client)
        {
            _initializationConfiguration = initializationConfiguration ?? throw new ArgumentNullException(nameof(initializationConfiguration));
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        private readonly InitializationConfiguration _initializationConfiguration;
        private readonly HttpClient _client;

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
            using (var request = CreateRequest(query))
            using (var response = await _client.SendAsync(request, HttpCompletionOption.ResponseContentRead, token))
            {
                return await response.Content.ReadAsStringAsync();
            }
        }

        private HttpRequestMessage CreateRequest(string query)
        {
            var message = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = _initializationConfiguration.GitHubGraphQLQueryEndpoint,
                Content = new StringContent(query, Encoding.UTF8, "application/json")
            };

            message.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", _initializationConfiguration.GitHubPersonalAccessToken);
            message.Headers.UserAgent.TryParseAdd(UserAgent);
            return message;
        }
    }
}