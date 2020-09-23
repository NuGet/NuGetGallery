// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Moq;
using Octokit;
using Xunit;

namespace NuGet.Jobs.GitHubIndexer.Tests
{
    public class GitHubSearchWrapperFacts
    {
        private static GitHubSearchWrapper GetTestSearcher(IReadOnlyDictionary<string, string> headers = null)
        {
            var mockClient = new Mock<IGitHubClient>();
            var mockConnection = new Mock<IConnection>();
            var mockApiResponse = new Mock<IApiResponse<SearchRepositoryResult>>();
            var mockResponse = new Mock<IResponse>();

            mockApiResponse.Setup(x => x.HttpResponse)
                .Returns(mockResponse.Object);
            mockApiResponse.Setup(x => x.Body)
                .Returns(new SearchRepositoryResult(totalCount: 0, incompleteResults: false, items: new List<Repository>()));
            mockResponse
                .Setup(x => x.Headers)
                .Returns(headers);

            mockClient
                .SetupGet(x => x.Connection)
                .Returns(mockConnection.Object);

            mockConnection
                .Setup(x => x.Get<SearchRepositoryResult>(It.IsAny<Uri>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<string>()))
                .ReturnsAsync((Uri uri, IDictionary<string, string> parameters, string accepts) =>
                {
                    return mockApiResponse.Object;
                });
            return new GitHubSearchWrapper(mockClient.Object);
        }

        public class GetResponseMethod
        {
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
