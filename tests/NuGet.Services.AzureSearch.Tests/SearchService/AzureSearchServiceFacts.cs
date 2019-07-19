// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Search.Models;
using Moq;
using NuGet.Services.AzureSearch.Wrappers;
using Xunit;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class AzureSearchServiceFacts
    {
        public class V2SearchAsync : BaseFacts
        {
            [Fact]
            public async Task CallsDependenciesProperlyWithoutHijack()
            {
                _v2Request.IgnoreFilter = false;

                var response = await _target.V2SearchAsync(_v2Request);

                Assert.Same(_v2Response, response);
                _textBuilder.Verify(
                    x => x.V2Search(_v2Request),
                    Times.Once);
                _parametersBuilder.Verify(
                    x => x.GetSearchFilters(It.IsAny<SearchRequest>()),
                    Times.Never);
                _parametersBuilder.Verify(
                    x => x.V2Search(_v2Request),
                    Times.Once);
                _searchOperations.Verify(
                    x => x.SearchAsync<SearchDocument.Full>(_v2Parsed.Text, _v2Parameters),
                    Times.Once);
                _responseBuilder.Verify(
                    x => x.V2FromSearch(_v2Request, _v2Parameters, _v2Parsed.Text, _searchResult, It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
                    Times.Once);
            }

            [Fact]
            public async Task CallsDependenciesProperlyWithHijack()
            {
                _v2Request.IgnoreFilter = true;

                var response = await _target.V2SearchAsync(_v2Request);

                Assert.Same(_v2Response, response);
                _textBuilder.Verify(
                    x => x.V2Search(_v2Request),
                    Times.Once);
                _parametersBuilder.Verify(
                    x => x.GetSearchFilters(It.IsAny<SearchRequest>()),
                    Times.Never);
                _parametersBuilder.Verify(
                    x => x.V2Search(_v2Request),
                    Times.Once);
                _hijackOperations.Verify(
                    x => x.SearchAsync<HijackDocument.Full>(_v2Parsed.Text, _v2Parameters),
                    Times.Once);
                _responseBuilder.Verify(
                    x => x.V2FromHijack(_v2Request, _v2Parameters, _v2Parsed.Text, _hijackResult, It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
                    Times.Once);
            }
        }

        public class V3SearchAsync : BaseFacts
        {
            [Fact]
            public async Task SearchesWithNoSpecificPackageId()
            {
                _v3Request.Skip = 0;
                _v3Request.Take = 1;
                _v3Parsed = new ParsedQuery(string.Empty, packageId: null);

                var response = await _target.V3SearchAsync(_v3Request);

                VerifyV3Search(response);
            }

            [Fact]
            public async Task SearchesWithSpecificPackageIdAndSkip()
            {
                _v3Request.Skip = 1;
                _v3Request.Take = 1;
                _v3Parsed = new ParsedQuery(string.Empty, _packageId);

                var response = await _target.V3SearchAsync(_v3Request);

                VerifyV3Search(response);
            }

            [Fact]
            public async Task SearchesWithSpecificPackageIdAndNoTake()
            {
                _v3Request.Skip = 0;
                _v3Request.Take = 0;
                _v3Parsed = new ParsedQuery(string.Empty, _packageId);

                var response = await _target.V3SearchAsync(_v3Request);

                VerifyV3Search(response);
            }

            [Fact]
            public async Task GetsDocumentWithSpecificPackageIdNoSkipAndSomeTake()
            {
                _v3Request.Skip = 0;
                _v3Request.Take = 1;
                _v3Parsed = new ParsedQuery(string.Empty, _packageId);

                var response = await _target.V3SearchAsync(_v3Request);

                VerifyV3SearchDocument(response);
            }

            private void VerifyV3SearchDocument(V3SearchResponse response)
            {
                Assert.Same(_v3Response, response);
                _textBuilder.Verify(
                    x => x.V3Search(_v3Request),
                    Times.Once);
                _parametersBuilder.Verify(
                    x => x.GetSearchFilters(It.IsAny<SearchRequest>()),
                    Times.Once);
                _parametersBuilder.Verify(
                    x => x.V3Search(It.IsAny<V3SearchRequest>()),
                    Times.Never);
                _searchOperations.Verify(
                    x => x.SearchAsync<SearchDocument.Full>(It.IsAny<string>(), It.IsAny<SearchParameters>()),
                    Times.Never);
                _searchOperations.Verify(
                    x => x.GetOrNullAsync<SearchDocument.Full>(_searchDocument.Key),
                    Times.Once);
                _responseBuilder.Verify(
                    x => x.V3FromSearchDocument(_v3Request, _searchDocument.Key, _searchDocument, It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
                    Times.Once);
            }

            private void VerifyV3Search(V3SearchResponse response)
            {
                Assert.Same(_v3Response, response);
                _textBuilder.Verify(
                    x => x.V3Search(_v3Request),
                    Times.Once);
                _parametersBuilder.Verify(
                    x => x.GetSearchFilters(It.IsAny<SearchRequest>()),
                    Times.Never);
                _parametersBuilder.Verify(
                    x => x.V3Search(_v3Request),
                    Times.Once);
                _searchOperations.Verify(
                    x => x.SearchAsync<SearchDocument.Full>(_v3Parsed.Text, _v3Parameters),
                    Times.Once);
                _responseBuilder.Verify(
                    x => x.V3FromSearch(_v3Request, _v3Parameters, _v3Parsed.Text, _searchResult, It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
                    Times.Once);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly Mock<ISearchTextBuilder> _textBuilder;
            protected readonly Mock<ISearchParametersBuilder> _parametersBuilder;
            protected readonly Mock<ISearchIndexClientWrapper> _searchIndex;
            protected readonly Mock<IDocumentsOperationsWrapper> _searchOperations;
            protected readonly Mock<ISearchIndexClientWrapper> _hijackIndex;
            protected readonly Mock<IDocumentsOperationsWrapper> _hijackOperations;
            protected readonly Mock<ISearchResponseBuilder> _responseBuilder;
            protected readonly Mock<IAzureSearchTelemetryService> _telemetryService;
            protected readonly string _packageId;
            protected readonly V2SearchRequest _v2Request;
            protected readonly V3SearchRequest _v3Request;
            protected readonly ParsedQuery _v2Parsed;
            protected ParsedQuery _v3Parsed;
            protected readonly SearchParameters _v2Parameters;
            protected readonly SearchParameters _v3Parameters;
            protected readonly DocumentSearchResult<SearchDocument.Full> _searchResult;
            protected readonly SearchDocument.Full _searchDocument;
            protected readonly DocumentSearchResult<HijackDocument.Full> _hijackResult;
            protected readonly V2SearchResponse _v2Response;
            protected readonly V3SearchResponse _v3Response;
            protected readonly AzureSearchService _target;

            public BaseFacts()
            {
                _textBuilder = new Mock<ISearchTextBuilder>();
                _parametersBuilder = new Mock<ISearchParametersBuilder>();
                _searchIndex = new Mock<ISearchIndexClientWrapper>();
                _searchOperations = new Mock<IDocumentsOperationsWrapper>();
                _hijackIndex = new Mock<ISearchIndexClientWrapper>();
                _hijackOperations = new Mock<IDocumentsOperationsWrapper>();
                _responseBuilder = new Mock<ISearchResponseBuilder>();
                _telemetryService = new Mock<IAzureSearchTelemetryService>();

                _packageId = "NuGet.Versioning";
                _v2Request = new V2SearchRequest { Skip = 0, Take = 20 };
                _v3Request = new V3SearchRequest { Skip = 0, Take = 20 };
                _v2Parsed = new ParsedQuery("v2", packageId: null);
                _v3Parsed = new ParsedQuery("v3", packageId: null);
                _v2Parameters = new SearchParameters();
                _v3Parameters = new SearchParameters();
                _searchResult = new DocumentSearchResult<SearchDocument.Full>();
                _searchDocument = new SearchDocument.Full
                {
                    Key = DocumentUtilities.GetSearchDocumentKey(_packageId, SearchFilters.Default),
                };
                _hijackResult = new DocumentSearchResult<HijackDocument.Full>();
                _v2Response = new V2SearchResponse();
                _v3Response = new V3SearchResponse();

                _textBuilder
                    .Setup(x => x.V2Search(It.IsAny<V2SearchRequest>()))
                    .Returns(() => _v2Parsed);
                _textBuilder
                    .Setup(x => x.V3Search(It.IsAny<V3SearchRequest>()))
                    .Returns(() => _v3Parsed);
                _parametersBuilder
                    .Setup(x => x.V2Search(It.IsAny<V2SearchRequest>()))
                    .Returns(() => _v2Parameters);
                _parametersBuilder
                    .Setup(x => x.V3Search(It.IsAny<V3SearchRequest>()))
                    .Returns(() => _v3Parameters);

                // Set up the search to take at least one millisecond. This allows us to test the measured duration.
                _searchOperations
                    .Setup(x => x.SearchAsync<SearchDocument.Full>(It.IsAny<string>(), It.IsAny<SearchParameters>()))
                    .ReturnsAsync(() => _searchResult)
                    .Callback(() => Thread.Sleep(TimeSpan.FromMilliseconds(1)));

                _searchOperations
                    .Setup(x => x.GetOrNullAsync<SearchDocument.Full>(It.IsAny<string>()))
                    .ReturnsAsync(() => _searchDocument)
                    .Callback(() => Thread.Sleep(TimeSpan.FromMilliseconds(1)));

                _hijackOperations
                    .Setup(x => x.SearchAsync<HijackDocument.Full>(It.IsAny<string>(), It.IsAny<SearchParameters>()))
                    .ReturnsAsync(() => _hijackResult)
                    .Callback(() => Thread.Sleep(TimeSpan.FromMilliseconds(1)));

                _responseBuilder
                    .Setup(x => x.V2FromHijack(
                        It.IsAny<V2SearchRequest>(),
                        It.IsAny<SearchParameters>(),
                        It.IsAny<string>(),
                        It.IsAny<DocumentSearchResult<HijackDocument.Full>>(),
                        It.IsAny<TimeSpan>()))
                    .Returns(() => _v2Response);
                _responseBuilder
                    .Setup(x => x.V2FromSearch(
                        It.IsAny<V2SearchRequest>(),
                        It.IsAny<SearchParameters>(),
                        It.IsAny<string>(),
                        It.IsAny<DocumentSearchResult<SearchDocument.Full>>(),
                        It.IsAny<TimeSpan>()))
                    .Returns(() => _v2Response);
                _responseBuilder
                    .Setup(x => x.V3FromSearch(
                        It.IsAny<V3SearchRequest>(),
                        It.IsAny<SearchParameters>(),
                        It.IsAny<string>(),
                        It.IsAny<DocumentSearchResult<SearchDocument.Full>>(),
                        It.IsAny<TimeSpan>()))
                    .Returns(() => _v3Response);
                _responseBuilder
                    .Setup(x => x.V3FromSearchDocument(
                        It.IsAny<V3SearchRequest>(),
                        It.IsAny<string>(),
                        It.IsAny<SearchDocument.Full>(),
                        It.IsAny<TimeSpan>()))
                    .Returns(() => _v3Response);

                _searchIndex.Setup(x => x.Documents).Returns(() => _searchOperations.Object);
                _hijackIndex.Setup(x => x.Documents).Returns(() => _hijackOperations.Object);

                _target = new AzureSearchService(
                    _textBuilder.Object,
                    _parametersBuilder.Object,
                    _searchIndex.Object,
                    _hijackIndex.Object,
                    _responseBuilder.Object,
                    _telemetryService.Object);
            }
        }
    }
}
