// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using Octokit;
using Xunit;

namespace NuGet.Jobs.GitHubIndexer.Tests
{
    public class GitHubSearchWrapperFacts
    {
        public Mock<IOptionsSnapshot<GitHubIndexerConfiguration>> MockConfig { get; private set; }

        private static GitHubSearchWrapper GetTestSearcher(
            IReadOnlyDictionary<string, string> headers = null,
            Mock<IConnection> mockConnection = null,
            GitHubIndexerConfiguration config = null)
        {
            var mockClient = new Mock<IGitHubClient>();
            var mockApiResponse = new Mock<IApiResponse<SearchRepositoryResult>>();
            var mockResponse = new Mock<IResponse>();
            var mockConfig = new Mock<IOptionsSnapshot<GitHubIndexerConfiguration>>();
            mockConfig.Setup(x => x.Value).Returns(config ?? new GitHubIndexerConfiguration());

            mockApiResponse.Setup(x => x.HttpResponse)
                .Returns(mockResponse.Object);
            mockApiResponse.Setup(x => x.Body)
                .Returns(new SearchRepositoryResult(totalCount: 0, incompleteResults: false, items: new List<Repository>()));
            mockResponse
                .Setup(x => x.Headers)
                .Returns(headers);

            if (mockConnection == null)
            {
                mockConnection = new Mock<IConnection>();

                mockConnection
                    .Setup(x => x.Get<SearchRepositoryResult>(
                        It.IsAny<Uri>(),
                        It.IsAny<IDictionary<string, string>>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Uri uri, IDictionary<string, string> parameters, string accepts, CancellationToken token) =>
                    {
                        return mockApiResponse.Object;
                    });
            }

            mockClient
                .SetupGet(x => x.Connection)
                .Returns(mockConnection.Object);

            return new GitHubSearchWrapper(mockClient.Object, mockConfig.Object);
        }

        public class GetResponseMethod
        {
            [Fact]
            public async Task CancelsTheRequestAtTwiceTheTimeout()
            {
                var config = new GitHubIndexerConfiguration
                {
                    GitHubRequestTimeout = TimeSpan.FromMilliseconds(100),
                };
                var mockConnection = new Mock<IConnection>();

                mockConnection
                    .Setup(x => x.Get<SearchRepositoryResult>(
                        It.IsAny<Uri>(),
                        It.IsAny<IDictionary<string, string>>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()))
                    .Returns(async (Uri uri, IDictionary<string, string> parameters, string accepts, CancellationToken token) =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10), token);
                        throw new InvalidOperationException("A timeout should have happened first.");
                    });

                var searcher = GetTestSearcher(mockConnection: mockConnection, config: config);

                var sw = Stopwatch.StartNew();
                var ex = await Assert.ThrowsAsync<OperationCanceledException>(() => searcher.GetResponse(new SearchRepositoriesRequest { }));
                Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(200));
                Assert.Equal("The operation was forcibly canceled.", ex.Message);
            }

            [Fact]
            public async Task DoesNotThrowIfCaseInsensitiveHeader()
            {
                var headers = new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>()
                    {
                        { "dAtE", "Fri, 12 Oct 2012 23:33:14 GMT" },
                        { "x-RaTeLiMiT-rEsEt", "1350085394"},
                        { "RandomHeaderThatShouldntBeHere", "SomeRandomValue"},
                        { "@aghfkghfk", "SomeRandomValue"},
                    });
                var searcher = GetTestSearcher(headers);

                await searcher.GetResponse(new SearchRepositoriesRequest { });
            }

            [Fact]
            public async Task DoesNotThrowIfDuplicateCaseInsensitiveHeaders()
            {
                var headers = new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>()
                    {
                        { "dAtE", "Fri, 12 Oct 2012 23:33:14 GMT" },
                        { "DAtE", "Fri, 12 Oct 2012 23:33:14 GMT" },
                        { "x-RaTeLiMiT-rEsEt", "1350085394"},
                        { "x-RATELIMIT-RESET", "1350085394"},
                        { "RandomHeaderThatShouldntBeHere", "SomeRandomValue"},
                        { "@aghfkghfk", "SomeRandomValue"},
                    });
                var searcher = GetTestSearcher(headers);

                await searcher.GetResponse(new SearchRepositoriesRequest { });
            }

            [Fact]
            public async Task TestMissingDateHeader()
            {
                var headers = new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>()
                    {
                        { "x-RaTeLiMiT-rEsEt", "1350085394"},
                        { "RandomHeaderThatShouldntBeHere", "SomeRandomValue"},
                        { "@aghfkghfk", "SomeRandomValue"},
                    });
                var searcher = GetTestSearcher(headers);

                await Assert.ThrowsAsync<InvalidDataException>(async () =>
                {
                    await searcher.GetResponse(new SearchRepositoriesRequest { });
                });
            }

            [Fact]
            public async Task TestMissingRateLimitHeader()
            {
                var headers = new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>()
                    {
                        { "dAtE", "Fri, 12 Oct 2012 23:33:14 GMT" },
                        { "RandomHeaderThatShouldntBeHere", "SomeRandomValue"},
                        { "@aghfkghfk", "SomeRandomValue"},
                    });
                var searcher = GetTestSearcher(headers);

                await Assert.ThrowsAsync<InvalidDataException>(async () =>
                {
                    await searcher.GetResponse(new SearchRepositoriesRequest { });
                });
            }

            [Fact]
            public async Task TestInvalidDateFormat()
            {
                var headers = new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>()
                    {
                        { "dAtE", "Friday, 12 Oct 2012 23:33:14 GMT" },
                        { "x-RaTeLiMiT-rEsEt", "1350085394"},
                        { "RandomHeaderThatShouldntBeHere", "SomeRandomValue"},
                        { "@aghfkghfk", "SomeRandomValue"},
                    });
                var searcher = GetTestSearcher(headers);

                await Assert.ThrowsAsync<InvalidDataException>(async () =>
                {
                    await searcher.GetResponse(new SearchRepositoriesRequest { });
                });
            }

            [Fact]
            public async Task TestInvalidDateTimezone()
            {
                var headers = new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>()
                    {
                        { "dAtE", "Fri, 12 Oct 2012 23:33:14 UTC" },
                        { "x-RaTeLiMiT-rEsEt", "1350085394"},
                        { "RandomHeaderThatShouldntBeHere", "SomeRandomValue"},
                        { "@aghfkghfk", "SomeRandomValue"},
                    });
                var searcher = GetTestSearcher(headers);

                await Assert.ThrowsAsync<InvalidDataException>(async () =>
                {
                    await searcher.GetResponse(new SearchRepositoriesRequest { });
                });
            }

            [Fact]
            public async Task TestInvalidRateLimitValueType()
            {
                var headers = new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>()
                    {
                        { "dAtE", "Friday, 12 Oct 2012 23:33:14 GMT" },
                        { "x-RaTeLiMiT-rEsEt", "ThisShouldBeANumber"},
                        { "RandomHeaderThatShouldntBeHere", "SomeRandomValue"},
                        { "@aghfkghfk", "SomeRandomValue"},
                    });
                var searcher = GetTestSearcher(headers);

                await Assert.ThrowsAsync<InvalidDataException>(async () =>
                {
                    await searcher.GetResponse(new SearchRepositoriesRequest { });
                });
            }
        }

        [Fact]
        public async Task TestRateLimitOverflow()
        {
            var headers = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>()
                {
                        { "dAtE", "Friday, 12 Oct 2012 23:33:14 GMT" },
                        { "x-RaTeLiMiT-rEsEt", "13500853940000000000000000000000000000000000000000000000000000000000000000000000000"},
                        { "RandomHeaderThatShouldntBeHere", "SomeRandomValue"},
                        { "@aghfkghfk", "SomeRandomValue"},
                });
            var searcher = GetTestSearcher(headers);

            await Assert.ThrowsAsync<InvalidDataException>(async () =>
            {
                await searcher.GetResponse(new SearchRepositoriesRequest { });
            });
        }
    }
}
