// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Threading.Tasks;
using Xunit;
using System;
using System.Net.Http;
using BasicSearchTests.FunctionalTests.Core.Models;
using Newtonsoft.Json;

namespace BasicSearchTests.FunctionalTests.Core
{
    public class StartupFunctionalTests : BaseFunctionalTests
    {
        private static string RegistrationBaseUrl = "RegistrationsBaseUrl";
        private const int IndexDifferenceLimitInHrs = 1;

        [Fact]
        public async Task Ready()
        {
            // Act
            var response = await Client.GetAsync("/");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(content, "READY");
        }

        [Fact]
        public async Task InvalidEndpoint()
        {
            // Act
            var response = await Client.GetAsync("/invalid");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(content, "UNRECOGNIZED");
        }

        [Fact]
        public async Task IndexIsFresh()
        {

            var response = await Client.GetAsync("/search/diag");
            var content = await response.Content.ReadAsAsync<SearchDiagResult>();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(content);

            var lastRegistrationCommitTime = GetLastRegistrationCommitTime().Result;
            var diffTimes = lastRegistrationCommitTime.Subtract(content.CommitUserData.CommitTimeStamp).TotalHours;
            //Last CommitTimeStamp for search service shouldn't be far off from the last registration timestamp.
            Assert.True(diffTimes >= 0 && diffTimes <= IndexDifferenceLimitInHrs, "Search index is ahead of last registration timestamp");
        }

        private async Task<DateTime> GetLastRegistrationCommitTime()
        {
            var httpClient = new HttpClient(RetryHandler);
            var indexResponse = await httpClient.GetAsync(EnvironmentSettings.IndexBaseUrl);
            indexResponse.EnsureSuccessStatusCode();

            var endpointContent = await indexResponse.Content.ReadAsStringAsync();
            var endpointsList = JsonConvert.DeserializeObject<ServiceEndpointsList>(endpointContent);
            var resources = endpointsList.Resources;

            string registrationBaseUrl = null;
            for (int i = 0; i < resources.Count; i++)
            {
                if (resources[i].AtType == RegistrationBaseUrl)
                {
                    registrationBaseUrl = resources[i].AtId;
                    break;
                }
            }

            Assert.False(registrationBaseUrl == null, "Failed to get registration base url. Please check that index url is correct.");

            var cursorUri = new Uri(new Uri(registrationBaseUrl), "cursor.json");
            var registrationResponse = await httpClient.GetAsync(cursorUri);
            registrationResponse.EnsureSuccessStatusCode();

            var registrationCursorContent = await registrationResponse.Content.ReadAsStringAsync();
            var lastRegistrationCommitTime = JsonConvert.DeserializeObject<RegistrationCursor>(registrationCursorContent).Value;
            return lastRegistrationCommitTime;
        }
    }
}
