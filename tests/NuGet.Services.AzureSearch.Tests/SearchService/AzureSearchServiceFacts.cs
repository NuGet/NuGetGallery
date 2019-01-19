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
                    x => x.V2Search(_v2Request),
                    Times.Once);
                _searchOperations.Verify(
                    x => x.SearchAsync<SearchDocument.Full>(_v2Text, _v2Parameters),
                    Times.Once);
                _responseBuilder.Verify(
                    x => x.V2FromSearch(_v2Request, _v2Parameters, _v2Text, _searchResult, It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
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
                    x => x.V2Search(_v2Request),
                    Times.Once);
                _hijackOperations.Verify(
                    x => x.SearchAsync<HijackDocument.Full>(_v2Text, _v2Parameters),
                    Times.Once);
                _responseBuilder.Verify(
                    x => x.V2FromHijack(_v2Request, _v2Parameters, _v2Text, _hijackResult, It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
                    Times.Once);
            }
        }

        public class V3SearchAsync : BaseFacts
        {
            [Fact]
            public async Task CallsDependenciesProperly()
            {
                var response = await _target.V3SearchAsync(_v3Request);

                Assert.Same(_v3Response, response);
                _textBuilder.Verify(
                    x => x.V3Search(_v3Request),
                    Times.Once);
                _parametersBuilder.Verify(
                    x => x.V3Search(_v3Request),
                    Times.Once);
                _searchOperations.Verify(
                    x => x.SearchAsync<SearchDocument.Full>(_v3Text, _v3Parameters),
                    Times.Once);
                _responseBuilder.Verify(
                    x => x.V3FromSearch(_v3Request, _v3Parameters, _v3Text, _searchResult, It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
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
            protected readonly V2SearchRequest _v2Request;
            protected readonly V3SearchRequest _v3Request;
            protected readonly string _v2Text;
            protected readonly string _v3Text;
            protected readonly SearchParameters _v2Parameters;
            protected readonly SearchParameters _v3Parameters;
            protected readonly DocumentSearchResult<SearchDocument.Full> _searchResult;
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

                _v2Request = new V2SearchRequest();
                _v3Request = new V3SearchRequest();
                _v2Text = "v2";
                _v3Text = "v3";
                _v2Parameters = new SearchParameters();
                _v3Parameters = new SearchParameters();
                _searchResult = new DocumentSearchResult<SearchDocument.Full>();
                _hijackResult = new DocumentSearchResult<HijackDocument.Full>();
                _v2Response = new V2SearchResponse();
                _v3Response = new V3SearchResponse();

                _textBuilder
                    .Setup(x => x.V2Search(It.IsAny<V2SearchRequest>()))
                    .Returns(() => _v2Text);
                _textBuilder
                    .Setup(x => x.V3Search(It.IsAny<V3SearchRequest>()))
                    .Returns(() => _v3Text);
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

                _searchIndex.Setup(x => x.Documents).Returns(() => _searchOperations.Object);
                _hijackIndex.Setup(x => x.Documents).Returns(() => _hijackOperations.Object);

                _target = new AzureSearchService(
                    _textBuilder.Object,
                    _parametersBuilder.Object,
                    _searchIndex.Object,
                    _hijackIndex.Object,
                    _responseBuilder.Object);
            }
        }
    }
}
