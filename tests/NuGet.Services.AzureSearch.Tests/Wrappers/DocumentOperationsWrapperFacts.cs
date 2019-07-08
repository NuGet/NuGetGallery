// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Moq;
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

            public override async Task ExecuteAsync()
            {
                await Target.IndexAsync(new IndexBatch<object>(Enumerable.Empty<IndexAction<object>>()));
            }
        }

        public class SearchAsync : Facts
        {
            public SearchAsync(ITestOutputHelper output) : base(output)
            {
            }

            public override async Task ExecuteAsync()
            {
                await Target.SearchAsync(string.Empty, new SearchParameters());
            }
        }

        public class SearchAsyncOfT : Facts
        {
            public SearchAsyncOfT(ITestOutputHelper output) : base(output)
            {
            }

            public override async Task ExecuteAsync()
            {
                await Target.SearchAsync<object>(string.Empty, new SearchParameters());
            }
        }

        public class CountAsync : Facts
        {
            public CountAsync(ITestOutputHelper output) : base(output)
            {
            }

            public override async Task ExecuteAsync()
            {
                await Target.CountAsync();
            }
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

            [Fact]
            public async Task RetriesOnNullReferenceException()
            {
                DocumentOperations
                    .Setup(x => x.IndexWithHttpMessagesAsync<object>(
                        It.IsAny<IndexBatch<object>>(),
                        It.IsAny<SearchRequestOptions>(),
                        It.IsAny<Dictionary<string, List<string>>>(),
                        It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new NullReferenceException());
                DocumentOperations
                    .Setup(x => x.SearchWithHttpMessagesAsync(
                        It.IsAny<string>(),
                        It.IsAny<SearchParameters>(),
                        It.IsAny<SearchRequestOptions>(),
                        It.IsAny<Dictionary<string, List<string>>>(),
                        It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new NullReferenceException());
                DocumentOperations
                    .Setup(x => x.SearchWithHttpMessagesAsync<object>(
                        It.IsAny<string>(),
                        It.IsAny<SearchParameters>(),
                        It.IsAny<SearchRequestOptions>(),
                        It.IsAny<Dictionary<string, List<string>>>(),
                        It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new NullReferenceException());
                DocumentOperations
                    .Setup(x => x.CountWithHttpMessagesAsync(
                        It.IsAny<SearchRequestOptions>(),
                        It.IsAny<Dictionary<string, List<string>>>(),
                        It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new NullReferenceException());

                var ex = await Assert.ThrowsAsync<AzureSearchException>(() => ExecuteAsync());

                Assert.Equal(3, DocumentOperations.Invocations.Count);
                Assert.Equal(2, Logger.Messages.Count);
                Assert.Equal("The search query failed due to Azure/azure-sdk-for-net#3224.", ex.Message);
            }

            public abstract Task ExecuteAsync();
        }
    }
}
