// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using NuGet.Jobs.Monitoring.PackageLag;
using NuGet.Services.AzureManagement;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Monitoring.PackageLag.Tests
{
    public class SearchServiceClientFacts
    {
        private Instance _luceneInstance;
        private Instance _azureSearchInstance;
        private IAzureManagementAPIWrapper _azureApiWrapper;
        private IOptionsSnapshot<SearchServiceConfiguration> _options;
        private ILogger<SearchServiceClient> _logger;

        public SearchServiceClientFacts()
        {
            _luceneInstance = new Instance("production", 0, "Lucene-DiagUrl", "Lucene-BaseQueryUrl", "USNC", ServiceType.LuceneSearch);
            _azureSearchInstance = new Instance("production", 0, "Azure-DiagUrl", "Azure-BaseQueryUrl", "USNC", ServiceType.AzureSearch);


            var azureApiMock = new Mock<IAzureManagementAPIWrapper>();
            var configMock = new Mock<IOptionsSnapshot<SearchServiceConfiguration>>();
            var loggerMock = new Mock<ILogger<SearchServiceClient>>();

            configMock.Setup(cm => cm.Value)
                .Returns(new SearchServiceConfiguration
                {
                    InstancePortMinimum = 100
                });

            _azureApiWrapper = azureApiMock.Object;
            _options = configMock.Object;
            _logger = loggerMock.Object;
        }

        [Fact]
        public async Task CommitTimeStampDataIsCorrect()
        {
            // Arrange
            var token = new CancellationToken();
            var httpClientMock = new Mock<IHttpClientWrapper>();
            var luceneExpectedTicks = 5;
            httpClientMock.Setup(hcm => hcm.GetAsync(It.Is<string>(it => it.Equals("Lucene-DiagUrl")), HttpCompletionOption.ResponseContentRead, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IHttpResponseMessageWrapper)new TestHttpResponseMessage(HttpStatusCode.OK, JsonConvert.SerializeObject(new SearchDiagnosticResponse
                {
                    CommitUserData = new CommitUserData
                    {
                        CommitTimeStamp = new DateTimeOffset(luceneExpectedTicks, new TimeSpan(0)).ToString()
                    },
                    LastIndexReloadTime = new DateTimeOffset(luceneExpectedTicks, new TimeSpan(0))
                }))));
            httpClientMock.Setup(hcm => hcm.GetAsync(It.Is<string>(it => it.Equals("Azure-DiagUrl")), HttpCompletionOption.ResponseContentRead, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IHttpResponseMessageWrapper)new TestHttpResponseMessage(HttpStatusCode.OK, JsonConvert.SerializeObject(new AzureSearchDiagnosticResponse
                {
                    SearchIndex = new IndexInformation
                    {
                        LastCommitTimestamp = new DateTimeOffset()
                    }
                }))));
            var searchClient = new SearchServiceClient(_azureApiWrapper, httpClientMock.Object, _options, _logger);

            var azureStartTimestamp = DateTime.UtcNow;

            var luceneResponse = await searchClient.GetIndexLastReloadTimeAsync(_luceneInstance, token);
            var azureResponse = await searchClient.GetIndexLastReloadTimeAsync(_azureSearchInstance, token);

            var azureStopTimestamp = DateTime.UtcNow;

            Assert.InRange(azureResponse, azureStartTimestamp, azureStopTimestamp);
            Assert.Equal(luceneResponse, new DateTimeOffset(luceneExpectedTicks, new TimeSpan(0)));
        }

        private class TestHttpResponseMessage : IHttpResponseMessageWrapper
        {
            public TestHttpResponseMessage(HttpStatusCode response, string content)
            {
                StatusCode = response;
                var contentMock = new Mock<IHttpContentWrapper>();
                contentMock.Setup(cm => cm.ReadAsStringAsync())
                    .Returns(Task.FromResult(content));

                Content = contentMock.Object;
            }

            public bool IsSuccessStatusCode { get { return StatusCode.Equals(HttpStatusCode.OK); } set { } }

            public string ReasonPhrase { get; set; }
            public HttpStatusCode StatusCode { get; set; }

            public IHttpContentWrapper Content { get; set; }

            public void Dispose() { }
        }
    }
}
