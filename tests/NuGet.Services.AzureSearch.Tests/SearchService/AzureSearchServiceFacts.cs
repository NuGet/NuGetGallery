// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Search.Models;
using Moq;
using NuGet.Services.AzureSearch.Wrappers;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class AzureSearchServiceFacts
    {
        public class V2SearchAsync : BaseFacts
        {
            [Fact]
            public async Task SearchIndexAndEmptyOperation()
            {
                _v2Request.IgnoreFilter = false;
                _operation = IndexOperation.Empty();

                var response = await _target.V2SearchAsync(_v2Request);

                Assert.Same(_v2Response, response);
                _operationBuilder.Verify(
                    x => x.V2SearchWithSearchIndex(_v2Request),
                    Times.Once);
                _searchOperations.Verify(
                    x => x.SearchAsync<SearchDocument.Full>(It.IsAny<string>(), It.IsAny<SearchParameters>()),
                    Times.Never);
                _responseBuilder.Verify(
                    x => x.EmptyV2(_v2Request),
                    Times.Once);
            }

            [Fact]
            public async Task SearchIndexAndSearchOperation()
            {
                _v2Request.IgnoreFilter = false;

                var response = await _target.V2SearchAsync(_v2Request);

                Assert.Same(_v2Response, response);
                _operationBuilder.Verify(
                    x => x.V2SearchWithSearchIndex(_v2Request),
                    Times.Once);
                _searchOperations.Verify(
                    x => x.SearchAsync<SearchDocument.Full>(_text, _parameters),
                    Times.Once);
                _responseBuilder.Verify(
                    x => x.V2FromSearch(_v2Request, _text, _parameters, _searchResult, It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
                    Times.Once);
                _telemetryService.Verify(
                    x => x.TrackV2SearchQueryWithSearchIndex(It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
                    Times.Once);
            }

            [Fact]
            public async Task SearchIndexAndGetOperation()
            {
                _v2Request.IgnoreFilter = false;
                _operation = IndexOperation.Get(_key);

                var response = await _target.V2SearchAsync(_v2Request);

                Assert.Same(_v2Response, response);
                _operationBuilder.Verify(
                    x => x.V2SearchWithSearchIndex(_v2Request),
                    Times.Once);
                _searchOperations.Verify(
                    x => x.GetOrNullAsync<SearchDocument.Full>(_key),
                    Times.Once);
                _responseBuilder.Verify(
                    x => x.V2FromSearchDocument(_v2Request, _key, _searchDocument, It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
                    Times.Once);
                _telemetryService.Verify(
                    x => x.TrackV2GetDocumentWithSearchIndex(It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
                    Times.Once);
            }

            [Fact]
            public async Task HijackIndexAndEmptyOperation()
            {
                _v2Request.IgnoreFilter = true;
                _operation = IndexOperation.Empty();

                var response = await _target.V2SearchAsync(_v2Request);

                Assert.Same(_v2Response, response);
                _operationBuilder.Verify(
                    x => x.V2SearchWithHijackIndex(_v2Request),
                    Times.Once);
                _searchOperations.Verify(
                    x => x.SearchAsync<HijackDocument.Full>(It.IsAny<string>(), It.IsAny<SearchParameters>()),
                    Times.Never);
                _responseBuilder.Verify(
                    x => x.EmptyV2(_v2Request),
                    Times.Once);
            }

            [Fact]
            public async Task HijackIndexAndSearchOperation()
            {
                _v2Request.IgnoreFilter = true;

                var response = await _target.V2SearchAsync(_v2Request);

                Assert.Same(_v2Response, response);
                _operationBuilder.Verify(
                    x => x.V2SearchWithHijackIndex(_v2Request),
                    Times.Once);
                _hijackOperations.Verify(
                    x => x.SearchAsync<HijackDocument.Full>(_text, _parameters),
                    Times.Once);
                _responseBuilder.Verify(
                    x => x.V2FromHijack(_v2Request, _text, _parameters, _hijackResult, It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
                    Times.Once);
                _telemetryService.Verify(
                    x => x.TrackV2SearchQueryWithHijackIndex(It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
                    Times.Once);
            }

            [Theory]
            [InlineData(false, false, false)]
            [InlineData(true, false, false)]
            [InlineData(false, true, true)]
            [InlineData(true, true, true)]
            public async Task HijackIndexAndGetOperation(bool includePrerelease, bool includeSemVer2, bool returned)
            {
                _v2Request.IgnoreFilter = true;
                _v2Request.IncludePrerelease = includePrerelease;
                _v2Request.IncludeSemVer2 = includeSemVer2;
                _hijackDocument.Prerelease = true;
                _hijackDocument.SemVerLevel = SemVerLevelKey.SemVer2;
                _operation = IndexOperation.Get(_key);
                var expectedDocument = returned ? _hijackDocument : null;

                var response = await _target.V2SearchAsync(_v2Request);

                Assert.Same(_v2Response, response);
                _operationBuilder.Verify(
                    x => x.V2SearchWithHijackIndex(_v2Request),
                    Times.Once);
                _hijackOperations.Verify(
                    x => x.GetOrNullAsync<HijackDocument.Full>(_key),
                    Times.Once);
                _responseBuilder.Verify(
                    x => x.V2FromHijackDocument(_v2Request, _key, expectedDocument, It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
                    Times.Once);
                _telemetryService.Verify(
                    x => x.TrackV2GetDocumentWithHijackIndex(It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
                    Times.Once);
            }
        }

        public class V3SearchAsync : BaseFacts
        {
            [Fact]
            public async Task SearchIndexAndEmptyOperation()
            {
                _operation = IndexOperation.Empty();

                var response = await _target.V3SearchAsync(_v3Request);

                Assert.Same(_v3Response, response);
                _operationBuilder.Verify(
                    x => x.V3Search(_v3Request),
                    Times.Once);
                _searchOperations.Verify(
                    x => x.SearchAsync<SearchDocument.Full>(It.IsAny<string>(), It.IsAny<SearchParameters>()),
                    Times.Never);
                _responseBuilder.Verify(
                    x => x.EmptyV3(_v3Request),
                    Times.Once);
            }

            [Fact]
            public async Task SearchIndexAndSearchOperation()
            {
                var response = await _target.V3SearchAsync(_v3Request);

                Assert.Same(_v3Response, response);
                _operationBuilder.Verify(
                    x => x.V3Search(_v3Request),
                    Times.Once);
                _searchOperations.Verify(
                    x => x.SearchAsync<SearchDocument.Full>(_text, _parameters),
                    Times.Once);
                _responseBuilder.Verify(
                    x => x.V3FromSearch(_v3Request, _text, _parameters, _searchResult, It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
                    Times.Once);
                _telemetryService.Verify(
                    x => x.TrackV3SearchQuery(It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
                    Times.Once);
            }

            [Fact]
            public async Task SearchIndexAndGetOperation()
            {
                _operation = IndexOperation.Get(_key);

                var response = await _target.V3SearchAsync(_v3Request);

                Assert.Same(_v3Response, response);
                _operationBuilder.Verify(
                    x => x.V3Search(_v3Request),
                    Times.Once);
                _searchOperations.Verify(
                    x => x.GetOrNullAsync<SearchDocument.Full>(_key),
                    Times.Once);
                _responseBuilder.Verify(
                    x => x.V3FromSearchDocument(_v3Request, _key, _searchDocument, It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
                    Times.Once);
                _telemetryService.Verify(
                    x => x.TrackV3GetDocument(It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
                    Times.Once);
            }
        }

        public class AutocompleteAsync : BaseFacts
        {
            [Fact]
            public async Task SearchIndexAndEmptyOperation()
            {
                _operation = IndexOperation.Empty();

                var response = await _target.AutocompleteAsync(_autocompleteRequest);

                Assert.Same(_autocompleteResponse, response);
                _operationBuilder.Verify(
                    x => x.Autocomplete(_autocompleteRequest),
                    Times.Once);
                _searchOperations.Verify(
                    x => x.SearchAsync<SearchDocument.Full>(It.IsAny<string>(), It.IsAny<SearchParameters>()),
                    Times.Never);
                _responseBuilder.Verify(
                    x => x.EmptyAutocomplete(_autocompleteRequest),
                    Times.Once);
            }

            [Fact]
            public async Task SearchIndexAndSearchOperation()
            {
                var response = await _target.AutocompleteAsync(_autocompleteRequest);

                Assert.Same(_autocompleteResponse, response);
                _operationBuilder.Verify(
                    x => x.Autocomplete(_autocompleteRequest),
                    Times.Once);
                _searchOperations.Verify(
                    x => x.SearchAsync<SearchDocument.Full>(_text, _parameters),
                    Times.Once);
                _responseBuilder.Verify(
                    x => x.AutocompleteFromSearch(_autocompleteRequest, _text, _parameters, _searchResult, It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
                    Times.Once);
                _telemetryService.Verify(
                    x => x.TrackAutocompleteQuery(It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
                    Times.Once);
            }

            [Fact]
            public async Task SearchIndexAndGetOperation()
            {
                _operation = IndexOperation.Get(_key);

                var ex = await Assert.ThrowsAsync<NotImplementedException>(() => _target.AutocompleteAsync(_autocompleteRequest));
                Assert.Equal("The operation type Get is not supported.", ex.Message);
                _operationBuilder.Verify(
                    x => x.Autocomplete(_autocompleteRequest),
                    Times.Once);
                _searchOperations.Verify(
                    x => x.GetOrNullAsync<SearchDocument.Full>(It.IsAny<string>()),
                    Times.Never);
                _telemetryService.Verify(
                    x => x.TrackV3GetDocument(It.IsAny<TimeSpan>()),
                    Times.Never);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly Mock<IIndexOperationBuilder> _operationBuilder;
            protected readonly Mock<ISearchIndexClientWrapper> _searchIndex;
            protected readonly Mock<IDocumentsOperationsWrapper> _searchOperations;
            protected readonly Mock<ISearchIndexClientWrapper> _hijackIndex;
            protected readonly Mock<IDocumentsOperationsWrapper> _hijackOperations;
            protected readonly Mock<ISearchResponseBuilder> _responseBuilder;
            protected readonly Mock<IAzureSearchTelemetryService> _telemetryService;
            protected readonly V2SearchRequest _v2Request;
            protected readonly V3SearchRequest _v3Request;
            protected readonly AutocompleteRequest _autocompleteRequest;
            protected readonly string _key;
            protected readonly string _text;
            protected readonly SearchParameters _parameters;
            protected IndexOperation _operation;
            protected readonly DocumentSearchResult<SearchDocument.Full> _searchResult;
            protected readonly SearchDocument.Full _searchDocument;
            protected readonly DocumentSearchResult<HijackDocument.Full> _hijackResult;
            protected readonly HijackDocument.Full _hijackDocument;
            protected readonly V2SearchResponse _v2Response;
            protected readonly V3SearchResponse _v3Response;
            protected readonly AutocompleteResponse _autocompleteResponse;
            protected readonly AzureSearchService _target;

            public BaseFacts()
            {
                _operationBuilder = new Mock<IIndexOperationBuilder>();
                _searchIndex = new Mock<ISearchIndexClientWrapper>();
                _searchOperations = new Mock<IDocumentsOperationsWrapper>();
                _hijackIndex = new Mock<ISearchIndexClientWrapper>();
                _hijackOperations = new Mock<IDocumentsOperationsWrapper>();
                _responseBuilder = new Mock<ISearchResponseBuilder>();
                _telemetryService = new Mock<IAzureSearchTelemetryService>();

                _v2Request = new V2SearchRequest();
                _v3Request = new V3SearchRequest();
                _autocompleteRequest = new AutocompleteRequest();
                _key = "key";
                _text = "search";
                _parameters = new SearchParameters();
                _operation = IndexOperation.Search(_text, _parameters);
                _searchResult = new DocumentSearchResult<SearchDocument.Full>();
                _searchDocument = new SearchDocument.Full();
                _hijackResult = new DocumentSearchResult<HijackDocument.Full>();
                _hijackDocument = new HijackDocument.Full();
                _v2Response = new V2SearchResponse();
                _v3Response = new V3SearchResponse();
                _autocompleteResponse = new AutocompleteResponse();

                _operationBuilder
                    .Setup(x => x.V2SearchWithHijackIndex(It.IsAny<V2SearchRequest>()))
                    .Returns(() => _operation);
                _operationBuilder
                    .Setup(x => x.V2SearchWithSearchIndex(It.IsAny<V2SearchRequest>()))
                    .Returns(() => _operation);
                _operationBuilder
                    .Setup(x => x.V3Search(It.IsAny<V3SearchRequest>()))
                    .Returns(() => _operation);
                _operationBuilder
                    .Setup(x => x.Autocomplete(It.IsAny<AutocompleteRequest>()))
                    .Returns(() => _operation);

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
                _hijackOperations
                    .Setup(x => x.GetOrNullAsync<HijackDocument.Full>(It.IsAny<string>()))
                    .ReturnsAsync(() => _hijackDocument)
                    .Callback(() => Thread.Sleep(TimeSpan.FromMilliseconds(1)));

                _responseBuilder
                    .Setup(x => x.V2FromHijack(
                        It.IsAny<V2SearchRequest>(),
                        It.IsAny<string>(),
                        It.IsAny<SearchParameters>(),
                        It.IsAny<DocumentSearchResult<HijackDocument.Full>>(),
                        It.IsAny<TimeSpan>()))
                    .Returns(() => _v2Response);
                _responseBuilder
                    .Setup(x => x.V2FromHijackDocument(
                        It.IsAny<V2SearchRequest>(),
                        It.IsAny<string>(),
                        It.IsAny<HijackDocument.Full>(),
                        It.IsAny<TimeSpan>()))
                    .Returns(() => _v2Response);
                _responseBuilder
                    .Setup(x => x.V2FromSearch(
                        It.IsAny<V2SearchRequest>(),
                        It.IsAny<string>(),
                        It.IsAny<SearchParameters>(),
                        It.IsAny<DocumentSearchResult<SearchDocument.Full>>(),
                        It.IsAny<TimeSpan>()))
                    .Returns(() => _v2Response);
                _responseBuilder
                    .Setup(x => x.V2FromSearchDocument(
                        It.IsAny<V2SearchRequest>(),
                        It.IsAny<string>(),
                        It.IsAny<SearchDocument.Full>(),
                        It.IsAny<TimeSpan>()))
                    .Returns(() => _v2Response);
                _responseBuilder
                    .Setup(x => x.V3FromSearch(
                        It.IsAny<V3SearchRequest>(),
                        It.IsAny<string>(),
                        It.IsAny<SearchParameters>(),
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
                _responseBuilder
                    .Setup(x => x.AutocompleteFromSearch(
                        It.IsAny<AutocompleteRequest>(),
                        It.IsAny<string>(),
                        It.IsAny<SearchParameters>(),
                        It.IsAny<DocumentSearchResult<SearchDocument.Full>>(),
                        It.IsAny<TimeSpan>()))
                    .Returns(() => _autocompleteResponse);
                _responseBuilder
                    .Setup(x => x.EmptyV2(It.IsAny<V2SearchRequest>()))
                    .Returns(() => _v2Response);
                _responseBuilder
                    .Setup(x => x.EmptyV3(It.IsAny<V3SearchRequest>()))
                    .Returns(() => _v3Response);
                _responseBuilder
                    .Setup(x => x.EmptyAutocomplete(It.IsAny<AutocompleteRequest>()))
                    .Returns(() => _autocompleteResponse);

                _searchIndex.Setup(x => x.Documents).Returns(() => _searchOperations.Object);
                _hijackIndex.Setup(x => x.Documents).Returns(() => _hijackOperations.Object);

                _target = new AzureSearchService(
                    _operationBuilder.Object,
                    _searchIndex.Object,
                    _hijackIndex.Object,
                    _responseBuilder.Object,
                    _telemetryService.Object);
            }
        }
    }
}
