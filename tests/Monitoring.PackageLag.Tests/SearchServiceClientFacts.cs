// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using NuGet.Jobs.Monitoring.PackageLag;
using Xunit;

namespace NuGet.Monitoring.PackageLag.Tests
{
    public class SearchServiceClientFacts
    {
        private Instance _azureSearchInstance;
        private ILogger<SearchServiceClient> _logger;

        public SearchServiceClientFacts()
        {
            _azureSearchInstance = new Instance("production", 0, "Azure-DiagUrl", "Azure-BaseQueryUrl", "USNC", ServiceType.AzureSearch);

            var loggerMock = new Mock<ILogger<SearchServiceClient>>();

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
            var searchClient = new SearchServiceClient(httpClientMock.Object, _logger);

            var azureStartTimestamp = DateTime.UtcNow;

            var azureResponse = await searchClient.GetIndexLastReloadTimeAsync(_azureSearchInstance, token);

            var azureStopTimestamp = DateTime.UtcNow;

            Assert.InRange(azureResponse, azureStartTimestamp, azureStopTimestamp);
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
