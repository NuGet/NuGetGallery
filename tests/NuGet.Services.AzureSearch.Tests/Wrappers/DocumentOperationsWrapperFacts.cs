// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Moq;
using Moq.Language;
using NuGet.Services.AzureSearch.SearchService;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.Wrappers
{
    public class DocumentOperationsWrapperFacts
    {
        public class IndexAsync : Facts
        {
            public IndexAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task DoesNotHandleNotFoundException()
            {
                SearchClient
                    .Setup(x => x.IndexDocumentsAsync<object>(
                        It.IsAny<IndexDocumentsBatch<object>>(),
                        It.IsAny<IndexDocumentsOptions>(),
                        It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new RequestFailedException((int)HttpStatusCode.NotFound, string.Empty));

                await Assert.ThrowsAsync<RequestFailedException>(
                    () => Target.IndexAsync(new IndexDocumentsBatch<object>()));
            }

            [Fact]
            public async Task DoesNotWrapIndexBatchException()
            {
                SearchClient
                    .Setup(x => x.IndexDocumentsAsync(
                        It.IsAny<IndexDocumentsBatch<object>>(),
                        It.IsAny<IndexDocumentsOptions>(),
                        It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new IndexBatchException(new List<IndexingResult>()));

                await Assert.ThrowsAsync<IndexBatchException>(
                    () => Target.IndexAsync(new IndexDocumentsBatch<object>()));
            }

            [Fact]
            public async Task DoesNotRetryOnNullReferenceException()
            {
                SearchClient
                    .Setup(x => x.IndexDocumentsAsync(
                        It.IsAny<IndexDocumentsBatch<object>>(),
                        It.IsAny<IndexDocumentsOptions>(),
                        It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new NullReferenceException());

                var ex = await Assert.ThrowsAsync<NullReferenceException>(
                    () => Target.IndexAsync(new IndexDocumentsBatch<object>()));

                Assert.Single(SearchClient.Invocations);
            }
        }

        public class GetOrNullAsync : RetryFacts<object, object>
        {
            public GetOrNullAsync(ITestOutputHelper output) : base(output)
            {
            }

            public override bool TreatsNotFoundAsDefault => true;

            public override async Task<object> ExecuteAsync()
            {
                return await Target.GetOrNullAsync<object>(string.Empty);
            }

            public override IReturns<SearchClient, Task<Response<object>>> Setup()
            {
                return SearchClient
                    .Setup(x => x.GetDocumentAsync<object>(
                        It.IsAny<string>(),
                        It.IsAny<GetDocumentOptions>(),
                        It.IsAny<CancellationToken>()));
            }
        }

        public class SearchAsync : RetryFacts<SearchResults<object>, SingleSearchResultPage<object>>
        {
            public SearchAsync(ITestOutputHelper output) : base(output)
            {
            }

            public override bool TreatsNotFoundAsDefault => false;

            public override async Task<SingleSearchResultPage<object>> ExecuteAsync()
            {
                return await Target.SearchAsync<object>(string.Empty, new SearchOptions());
            }

            public override IReturns<SearchClient, Task<Response<SearchResults<object>>>> Setup()
            {
                return SearchClient
                    .Setup(x => x.SearchAsync<object>(
                        It.IsAny<string>(),
                        It.IsAny<SearchOptions>(),
                        It.IsAny<CancellationToken>()));
            }
        }

        public class CountAsync : RetryFacts<long, long>
        {
            public CountAsync(ITestOutputHelper output) : base(output)
            {
            }

            public override bool TreatsNotFoundAsDefault => false;

            public override async Task<long> ExecuteAsync()
            {
                return await Target.CountAsync();
            }

            public override IReturns<SearchClient, Task<Response<long>>> Setup()
            {
                return SearchClient
                    .Setup(x => x.GetDocumentCountAsync(It.IsAny<CancellationToken>()));
            }
        }

        public abstract class RetryFacts<TIn, TOut> : Facts
        {
            public RetryFacts(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task HandlesNotFoundException()
            {
                Setup()
                    .ThrowsAsync(new RequestFailedException((int)HttpStatusCode.NotFound, string.Empty));

                if (TreatsNotFoundAsDefault)
                {
                    var result = await ExecuteAsync();
                    Assert.Equal(default(TOut), result);
                }
                else
                {
                    await Assert.ThrowsAsync<AzureSearchException>(() => ExecuteAsync());
                }
            }

            [Fact]
            public async Task RethrowsBadRequest()
            {
                Setup()
                    .ThrowsAsync(new RequestFailedException((int)HttpStatusCode.BadRequest, string.Empty));

                var ex = await Assert.ThrowsAsync<InvalidSearchRequestException>(() => ExecuteAsync());
                Assert.Equal("The provided query is invalid.", ex.Message);
                Assert.IsType<RequestFailedException>(ex.InnerException);
            }

            public abstract bool TreatsNotFoundAsDefault { get; }
            public abstract IReturns<SearchClient, Task<Response<TIn>>> Setup();
            public abstract Task<TOut> ExecuteAsync();
        }

        public abstract class Facts
        {
            public Facts(ITestOutputHelper output)
            {
                SearchClient = new Mock<SearchClient>();
                Target = new SearchClientWrapper(SearchClient.Object);
            }

            public Mock<SearchClient> SearchClient { get; }
            public SearchClientWrapper Target { get; }
        }
    }
}
