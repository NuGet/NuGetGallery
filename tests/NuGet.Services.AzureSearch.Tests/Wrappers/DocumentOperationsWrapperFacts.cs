// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Microsoft.Rest;
using Microsoft.Rest.Azure;
using Moq;
using Moq.Language;
using NuGet.Services.AzureSearch.Support;
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
                DocumentOperations
                    .Setup(x => x.IndexWithHttpMessagesAsync(
                        It.IsAny<IndexBatch<object>>(),
                        It.IsAny<SearchRequestOptions>(),
                        It.IsAny<Dictionary<string, List<string>>>(),
                        It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new CloudException
                    {
                        Response = new HttpResponseMessageWrapper(new HttpResponseMessage(HttpStatusCode.NotFound), string.Empty),
                    });

                await Assert.ThrowsAsync<CloudException>(
                    () => Target.IndexAsync(new IndexBatch<object>(Enumerable.Empty<IndexAction<object>>())));
            }

            [Fact]
            public async Task DoesNotWrapIndexBatchException()
            {
                DocumentOperations
                    .Setup(x => x.IndexWithHttpMessagesAsync(
                        It.IsAny<IndexBatch<object>>(),
                        It.IsAny<SearchRequestOptions>(),
                        It.IsAny<Dictionary<string, List<string>>>(),
                        It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new IndexBatchException(new DocumentIndexResult(new List<IndexingResult>())));

                await Assert.ThrowsAsync<IndexBatchException>(
                    () => Target.IndexAsync(new IndexBatch<object>(Enumerable.Empty<IndexAction<object>>())));
            }

            [Fact]
            public async Task DoesNotRetryOnNullReferenceException()
            {
                DocumentOperations
                    .Setup(x => x.IndexWithHttpMessagesAsync(
                        It.IsAny<IndexBatch<object>>(),
                        It.IsAny<SearchRequestOptions>(),
                        It.IsAny<Dictionary<string, List<string>>>(),
                        It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new NullReferenceException());

                var ex = await Assert.ThrowsAsync<NullReferenceException>(
                    () => Target.IndexAsync(new IndexBatch<object>(Enumerable.Empty<IndexAction<object>>())));

                Assert.Equal(1, DocumentOperations.Invocations.Count);
            }
        }

        public class GetOrNullAsyncOfT : RetryFacts<object>
        {
            public GetOrNullAsyncOfT(ITestOutputHelper output) : base(output)
            {
            }

            public override bool TreatsNotFoundAsDefault => true;

            public override async Task<object> ExecuteAsync()
            {
                return await Target.GetOrNullAsync<object>(string.Empty);
            }

            public override IReturns<IDocumentsOperations, Task<AzureOperationResponse<object>>> Setup()
            {
                return DocumentOperations
                    .Setup(x => x.GetWithHttpMessagesAsync<object>(
                        It.IsAny<string>(),
                        It.IsAny<IEnumerable<string>>(),
                        It.IsAny<SearchRequestOptions>(),
                        It.IsAny<Dictionary<string, List<string>>>(),
                        It.IsAny<CancellationToken>()));
            }
        }

        public class SearchAsync : RetryFacts<DocumentSearchResult>
        {
            public SearchAsync(ITestOutputHelper output) : base(output)
            {
            }

            public override bool TreatsNotFoundAsDefault => false;

            public override async Task<DocumentSearchResult> ExecuteAsync()
            {
                return await Target.SearchAsync(string.Empty, new SearchParameters());
            }

            public override IReturns<IDocumentsOperations, Task<AzureOperationResponse<DocumentSearchResult>>> Setup()
            {
                return DocumentOperations
                    .Setup(x => x.SearchWithHttpMessagesAsync(
                        It.IsAny<string>(),
                        It.IsAny<SearchParameters>(),
                        It.IsAny<SearchRequestOptions>(),
                        It.IsAny<Dictionary<string, List<string>>>(),
                        It.IsAny<CancellationToken>()));
            }
        }

        public class SearchAsyncOfT : RetryFacts<DocumentSearchResult<object>>
        {
            public SearchAsyncOfT(ITestOutputHelper output) : base(output)
            {
            }

            public override bool TreatsNotFoundAsDefault => false;

            public override async Task<DocumentSearchResult<object>> ExecuteAsync()
            {
                return await Target.SearchAsync<object>(string.Empty, new SearchParameters());
            }

            public override IReturns<IDocumentsOperations, Task<AzureOperationResponse<DocumentSearchResult<object>>>> Setup()
            {
                return DocumentOperations
                    .Setup(x => x.SearchWithHttpMessagesAsync<object>(
                        It.IsAny<string>(),
                        It.IsAny<SearchParameters>(),
                        It.IsAny<SearchRequestOptions>(),
                        It.IsAny<Dictionary<string, List<string>>>(),
                        It.IsAny<CancellationToken>()));
            }
        }

        public class CountAsync : RetryFacts<long>
        {
            public CountAsync(ITestOutputHelper output) : base(output)
            {
            }

            public override bool TreatsNotFoundAsDefault => false;

            public override async Task<long> ExecuteAsync()
            {
                return await Target.CountAsync();
            }

            public override IReturns<IDocumentsOperations, Task<AzureOperationResponse<long>>> Setup()
            {
                return DocumentOperations
                    .Setup(x => x.CountWithHttpMessagesAsync(
                        It.IsAny<SearchRequestOptions>(),
                        It.IsAny<Dictionary<string, List<string>>>(),
                        It.IsAny<CancellationToken>()));
            }
        }

        public abstract class RetryFacts<T> : Facts
        {
            public RetryFacts(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task HandlesNotFoundException()
            {
                Setup()
                    .ThrowsAsync(new CloudException
                    {
                        Response = new HttpResponseMessageWrapper(new HttpResponseMessage(HttpStatusCode.NotFound), string.Empty),
                    });

                if (TreatsNotFoundAsDefault)
                {
                    var result = await ExecuteAsync();
                    Assert.Equal(default(T), result);
                }
                else
                {
                    await Assert.ThrowsAsync<AzureSearchException>(() => ExecuteAsync());
                }
            }

            [Fact]
            public async Task RetriesOnNullReferenceException()
            {
                Setup()
                    .ThrowsAsync(new NullReferenceException());

                var ex = await Assert.ThrowsAsync<AzureSearchException>(() => ExecuteAsync());

                Assert.Equal(3, DocumentOperations.Invocations.Count);
                Assert.Equal(2, Logger.Messages.Count);
                Assert.Equal("The search query failed due to Azure/azure-sdk-for-net#3224.", ex.Message);
            }

            public abstract bool TreatsNotFoundAsDefault { get; }
            public abstract IReturns<IDocumentsOperations, Task<AzureOperationResponse<T>>> Setup();
            public abstract Task<T> ExecuteAsync();
        }

        public abstract class Facts
        {
            public Facts(ITestOutputHelper output)
            {
                DocumentOperations = new Mock<IDocumentsOperations>();
                Logger = output.GetLogger<DocumentsOperationsWrapper>();

                Target = new DocumentsOperationsWrapper(
                    DocumentOperations.Object,
                    Logger);
            }

            public Mock<IDocumentsOperations> DocumentOperations { get; }
            public RecordingLogger<DocumentsOperationsWrapper> Logger { get; }
            public DocumentsOperationsWrapper Target { get; }
        }
    }
}
