// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NgTests.Infrastructure;
using NuGet.Protocol.Catalog;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace CatalogTests
{
    public class CommitCollectorFacts
    {
        public class RunAsync : Facts
        {
            /// <summary>
            /// This is scenario #1 mentioned in <see cref="CommitCollector.GetCommitsInRange(IEnumerable{CatalogCommit}, DateTimeOffset, DateTimeOffset)"/>.
            /// </summary>
            [Fact]
            public async Task FetchesPageWhenBackCursorIsBeforeEnd()
            {
                Front.Value = DateTime.Parse("2019-11-04T00:30:00");
                Back.Value = DateTime.Parse("2019-11-04T02:30:00");

                var output = await Target.Object.RunAsync(Front, Back, Token);

                Assert.Equal(OrderedCommitIds.Skip(2).Take(6).ToArray(), Target.Object.Batches.Select(x => x.Single().CommitId).ToArray());
                HttpRetryStrategy.Verify(
                    x => x.SendAsync(It.IsAny<HttpClient>(), It.IsAny<Uri>(), It.IsAny<CancellationToken>()),
                    Times.Exactly(4));
                HttpRetryStrategy.Verify(x => x.SendAsync(It.IsAny<HttpClient>(), new Uri("https://example/catalog/index.json"), It.IsAny<CancellationToken>()));
                HttpRetryStrategy.Verify(x => x.SendAsync(It.IsAny<HttpClient>(), new Uri("https://example/catalog/page0.json"), It.IsAny<CancellationToken>()));
                HttpRetryStrategy.Verify(x => x.SendAsync(It.IsAny<HttpClient>(), new Uri("https://example/catalog/page1.json"), It.IsAny<CancellationToken>()));
                HttpRetryStrategy.Verify(x => x.SendAsync(It.IsAny<HttpClient>(), new Uri("https://example/catalog/page2.json"), It.IsAny<CancellationToken>()));
            }

            /// <summary>
            /// This is scenario #2 mentioned in <see cref="CommitCollector.GetCommitsInRange(IEnumerable{CatalogCommit}, DateTimeOffset, DateTimeOffset)"/>.
            /// </summary>
            [Fact]
            public async Task FetchesUpToNextPageWhenBackCursorIsAtTheEndOfTheNonLastPage()
            {
                Front.Value = DateTime.Parse("2019-11-04T01:01:00");
                Back.Value = DateTime.Parse("2019-11-04T03:00:00");

                var output = await Target.Object.RunAsync(Front, Back, Token);

                Assert.Equal(OrderedCommitIds.Skip(4).Take(5).ToArray(), Target.Object.Batches.Select(x => x.Single().CommitId).ToArray());
                HttpRetryStrategy.Verify(
                    x => x.SendAsync(It.IsAny<HttpClient>(), It.IsAny<Uri>(), It.IsAny<CancellationToken>()),
                    Times.Exactly(3));
                HttpRetryStrategy.Verify(x => x.SendAsync(It.IsAny<HttpClient>(), new Uri("https://example/catalog/index.json"), It.IsAny<CancellationToken>()));
                HttpRetryStrategy.Verify(x => x.SendAsync(It.IsAny<HttpClient>(), new Uri("https://example/catalog/page1.json"), It.IsAny<CancellationToken>()));
                HttpRetryStrategy.Verify(x => x.SendAsync(It.IsAny<HttpClient>(), new Uri("https://example/catalog/page2.json"), It.IsAny<CancellationToken>()));
            }

            /// <summary>
            /// This is scenario #3 mentioned in <see cref="CommitCollector.GetCommitsInRange(IEnumerable{CatalogCommit}, DateTimeOffset, DateTimeOffset)"/>.
            /// </summary>
            [Fact]
            public async Task SkipsPagesWhenFrontCursorIsAfterEnd()
            {
                Front.Value = DateTime.Parse("2019-11-04T01:00:00");
                Back.Value = DateTime.Parse("2019-11-04T02:01:00");

                var output = await Target.Object.RunAsync(Front, Back, Token);

                Assert.Equal(OrderedCommitIds.Skip(3).Take(4).ToArray(), Target.Object.Batches.Select(x => x.Single().CommitId).ToArray());
                HttpRetryStrategy.Verify(
                    x => x.SendAsync(It.IsAny<HttpClient>(), It.IsAny<Uri>(), It.IsAny<CancellationToken>()),
                    Times.Exactly(3));
                HttpRetryStrategy.Verify(x => x.SendAsync(It.IsAny<HttpClient>(), new Uri("https://example/catalog/index.json"), It.IsAny<CancellationToken>()));
                HttpRetryStrategy.Verify(x => x.SendAsync(It.IsAny<HttpClient>(), new Uri("https://example/catalog/page1.json"), It.IsAny<CancellationToken>()));
                HttpRetryStrategy.Verify(x => x.SendAsync(It.IsAny<HttpClient>(), new Uri("https://example/catalog/page2.json"), It.IsAny<CancellationToken>()));
            }

            [Fact]
            public async Task FetchesUpToNextPageWhenBackCursorIsAtTheBeginningOfTheNonLastPage()
            {
                Front.Value = DateTime.Parse("2019-11-04T00:30:00");
                Back.Value = DateTime.Parse("2019-11-04T01:01:00");

                var output = await Target.Object.RunAsync(Front, Back, Token);

                Assert.Equal(OrderedCommitIds.Skip(2).Take(2).ToArray(), Target.Object.Batches.Select(x => x.Single().CommitId).ToArray());
                HttpRetryStrategy.Verify(
                    x => x.SendAsync(It.IsAny<HttpClient>(), It.IsAny<Uri>(), It.IsAny<CancellationToken>()),
                    Times.Exactly(3));
                HttpRetryStrategy.Verify(x => x.SendAsync(It.IsAny<HttpClient>(), new Uri("https://example/catalog/index.json"), It.IsAny<CancellationToken>()));
                HttpRetryStrategy.Verify(x => x.SendAsync(It.IsAny<HttpClient>(), new Uri("https://example/catalog/page0.json"), It.IsAny<CancellationToken>()));
                HttpRetryStrategy.Verify(x => x.SendAsync(It.IsAny<HttpClient>(), new Uri("https://example/catalog/page1.json"), It.IsAny<CancellationToken>()));
            }

            [Fact]
            public async Task FetchesUpToNextPageWhenBackCursorIsInTheMiddleOfTheNonLastPage()
            {
                Front.Value = DateTime.Parse("2019-11-04T00:30:00");
                Back.Value = DateTime.Parse("2019-11-04T01:30:00");

                var output = await Target.Object.RunAsync(Front, Back, Token);

                Assert.Equal(OrderedCommitIds.Skip(2).Take(3).ToArray(), Target.Object.Batches.Select(x => x.Single().CommitId).ToArray());
                HttpRetryStrategy.Verify(
                    x => x.SendAsync(It.IsAny<HttpClient>(), It.IsAny<Uri>(), It.IsAny<CancellationToken>()),
                    Times.Exactly(3));
                HttpRetryStrategy.Verify(x => x.SendAsync(It.IsAny<HttpClient>(), new Uri("https://example/catalog/index.json"), It.IsAny<CancellationToken>()));
                HttpRetryStrategy.Verify(x => x.SendAsync(It.IsAny<HttpClient>(), new Uri("https://example/catalog/page0.json"), It.IsAny<CancellationToken>()));
                HttpRetryStrategy.Verify(x => x.SendAsync(It.IsAny<HttpClient>(), new Uri("https://example/catalog/page1.json"), It.IsAny<CancellationToken>()));
            }

            [Fact]
            public async Task CanFetchTheEntireCatalog()
            {
                var output = await Target.Object.RunAsync(Front, Back, Token);

                Assert.Equal(OrderedCommitIds.ToArray(), Target.Object.Batches.Select(x => x.Single().CommitId).ToArray());
                HttpRetryStrategy.Verify(
                    x => x.SendAsync(It.IsAny<HttpClient>(), It.IsAny<Uri>(), It.IsAny<CancellationToken>()),
                    Times.Exactly(5));
                HttpRetryStrategy.Verify(x => x.SendAsync(It.IsAny<HttpClient>(), new Uri("https://example/catalog/index.json"), It.IsAny<CancellationToken>()));
                HttpRetryStrategy.Verify(x => x.SendAsync(It.IsAny<HttpClient>(), new Uri("https://example/catalog/page0.json"), It.IsAny<CancellationToken>()));
                HttpRetryStrategy.Verify(x => x.SendAsync(It.IsAny<HttpClient>(), new Uri("https://example/catalog/page1.json"), It.IsAny<CancellationToken>()));
                HttpRetryStrategy.Verify(x => x.SendAsync(It.IsAny<HttpClient>(), new Uri("https://example/catalog/page2.json"), It.IsAny<CancellationToken>()));
                HttpRetryStrategy.Verify(x => x.SendAsync(It.IsAny<HttpClient>(), new Uri("https://example/catalog/page3.json"), It.IsAny<CancellationToken>()));
            }

            [Fact]
            public async Task ProcessesNoBatchesWhenCursorsAreCaughtUp()
            {
                Front.Value = DateTime.Parse("2019-11-04T04:00:00");

                var output = await Target.Object.RunAsync(Front, Back, Token);

                Assert.Empty(Target.Object.Batches);
                HttpRetryStrategy.Verify(
                    x => x.SendAsync(It.IsAny<HttpClient>(), It.IsAny<Uri>(), It.IsAny<CancellationToken>()),
                    Times.Once);
                HttpRetryStrategy.Verify(x => x.SendAsync(It.IsAny<HttpClient>(), new Uri("https://example/catalog/index.json"), It.IsAny<CancellationToken>()));
            }
        }

        public abstract class Facts
        {
            public Facts()
            {
                Index = new Uri("https://example/catalog/index.json");
                TelemetryService = new Mock<ITelemetryService>();
                Responses = new Dictionary<string, string>();
                HttpRetryStrategy = new Mock<IHttpRetryStrategy>();

                Front = MemoryCursor.CreateMin();
                Back = MemoryCursor.CreateMax();
                Token = CancellationToken.None;
                HttpRetryStrategy
                    .Setup(x => x.SendAsync(It.IsAny<HttpClient>(), It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                    .Returns<HttpClient, Uri, CancellationToken>((h, u, c) => h.GetAsync(u, c));

                OrderedCommitIds = new List<string>
                {
                    "51dd5ab3-d1e1-44db-bb94-7142685e4cb8",
                    "3219e7cd-e79d-400e-bfb8-74e35a07ee77",
                    "c426811f-9de9-4b4b-b206-73467eff859c",
                    "edcd04d8-9b9f-44b9-bfb5-0da2b5255487",
                    "de388ce4-f119-440c-97ad-e6a93e1a516b",
                    "c59d70fd-deeb-4afd-965a-a902add4e46f",
                    "7e5797a5-47b8-45af-9603-e7a120ebf7af",
                    "fe5850b6-e914-495e-87a0-14b9b9030a79",
                    "e9c20131-1f20-4a19-9900-7ed5a411a9f8",
                    "35e15b75-f296-4227-a1c6-4d389ef0d072",
                    "03eeddce-a058-434a-9046-012cb87da057",
                    "ddaed609-79ba-42e0-8be8-24fe87ba4647",
                };

                Responses[Index.AbsoluteUri] = Serialize(new CatalogIndex
                {
                    Items = new List<CatalogPageItem>
                    {
                        new CatalogPageItem
                        {
                            Url = "https://example/catalog/page0.json",
                            CommitTimestamp = DateTimeOffset.Parse("2019-11-04T01:00:00Z"),
                        },
                        new CatalogPageItem
                        {
                            Url = "https://example/catalog/page1.json",
                            CommitTimestamp = DateTimeOffset.Parse("2019-11-04T02:00:00Z"),
                        },
                        new CatalogPageItem
                        {
                            Url = "https://example/catalog/page2.json",
                            CommitTimestamp = DateTimeOffset.Parse("2019-11-04T03:00:00Z"),
                        },
                        new CatalogPageItem
                        {
                            Url = "https://example/catalog/page3.json",
                            CommitTimestamp = DateTimeOffset.Parse("2019-11-04T04:00:00Z"),
                        },
                    }
                });

                Responses["https://example/catalog/page0.json"] = Serialize(new CatalogPage
                {
                    Items = new List<CatalogLeafItem>
                    {
                        new CatalogLeafItem
                        {
                            Url = "https://example/catalog/item0.json",
                            PackageId = "NuGet.Versioning",
                            PackageVersion = "5.1.0",
                            Type = CatalogLeafType.PackageDetails,
                            CommitTimestamp = DateTimeOffset.Parse("2019-11-04T00:01:00Z"),
                            CommitId = OrderedCommitIds[0],
                        },
                        new CatalogLeafItem
                        {
                            Url = "https://example/catalog/item1.json",
                            PackageId = "NuGet.Versioning",
                            PackageVersion = "5.2.0",
                            Type = CatalogLeafType.PackageDetails,
                            CommitTimestamp = DateTimeOffset.Parse("2019-11-04T00:30:00Z"),
                            CommitId = OrderedCommitIds[1],
                        },
                        new CatalogLeafItem
                        {
                            Url = "https://example/catalog/item2.json",
                            PackageId = "NuGet.Versioning",
                            PackageVersion = "5.3.0",
                            Type = CatalogLeafType.PackageDetails,
                            CommitTimestamp = DateTimeOffset.Parse("2019-11-04T01:00:00Z"),
                            CommitId = OrderedCommitIds[2],
                        },
                    },
                    Context = PageContext,
                });

                Responses["https://example/catalog/page1.json"] = Serialize(new CatalogPage
                {
                    Items = new List<CatalogLeafItem>
                    {
                        new CatalogLeafItem
                        {
                            Url = "https://example/catalog/item3.json",
                            PackageId = "NuGet.Frameworks",
                            PackageVersion = "5.1.0",
                            Type = CatalogLeafType.PackageDetails,
                            CommitTimestamp = DateTimeOffset.Parse("2019-11-04T01:01:00Z"),
                            CommitId = OrderedCommitIds[3],
                        },
                        new CatalogLeafItem
                        {
                            Url = "https://example/catalog/item4.json",
                            PackageId = "NuGet.Frameworks",
                            PackageVersion = "5.2.0",
                            Type = CatalogLeafType.PackageDetails,
                            CommitTimestamp = DateTimeOffset.Parse("2019-11-04T01:30:00Z"),
                            CommitId = OrderedCommitIds[4],
                        },
                        new CatalogLeafItem
                        {
                            Url = "https://example/catalog/item5.json",
                            PackageId = "NuGet.Frameworks",
                            PackageVersion = "5.3.0",
                            Type = CatalogLeafType.PackageDetails,
                            CommitTimestamp = DateTimeOffset.Parse("2019-11-04T02:00:00Z"),
                            CommitId = OrderedCommitIds[5],
                        },
                    },
                    Context = PageContext,
                });

                Responses["https://example/catalog/page2.json"] = Serialize(new CatalogPage
                {
                    Items = new List<CatalogLeafItem>
                    {
                        new CatalogLeafItem
                        {
                            Url = "https://example/catalog/item6.json",
                            PackageId = "NuGet.Protocol",
                            PackageVersion = "5.1.0",
                            Type = CatalogLeafType.PackageDetails,
                            CommitTimestamp = DateTimeOffset.Parse("2019-11-04T02:01:00Z"),
                            CommitId = OrderedCommitIds[6],
                        },
                        new CatalogLeafItem
                        {
                            Url = "https://example/catalog/item7.json",
                            PackageId = "NuGet.Protocol",
                            PackageVersion = "5.2.0",
                            Type = CatalogLeafType.PackageDetails,
                            CommitTimestamp = DateTimeOffset.Parse("2019-11-04T02:30:00Z"),
                            CommitId = OrderedCommitIds[7],
                        },
                        new CatalogLeafItem
                        {
                            Url = "https://example/catalog/item8.json",
                            PackageId = "NuGet.Protocol",
                            PackageVersion = "5.3.0",
                            Type = CatalogLeafType.PackageDetails,
                            CommitTimestamp = DateTimeOffset.Parse("2019-11-04T03:00:00Z"),
                            CommitId = OrderedCommitIds[8],
                        },
                    },
                    Context = PageContext,
                });

                Responses["https://example/catalog/page3.json"] = Serialize(new CatalogPage
                {
                    Items = new List<CatalogLeafItem>
                    {
                        new CatalogLeafItem
                        {
                            Url = "https://example/catalog/item9.json",
                            PackageId = "NuGet.Commands",
                            PackageVersion = "5.1.0",
                            Type = CatalogLeafType.PackageDetails,
                            CommitTimestamp = DateTimeOffset.Parse("2019-11-04T03:01:00Z"),
                            CommitId = OrderedCommitIds[9],
                        },
                        new CatalogLeafItem
                        {
                            Url = "https://example/catalog/item10.json",
                            PackageId = "NuGet.Commands",
                            PackageVersion = "5.2.0",
                            Type = CatalogLeafType.PackageDetails,
                            CommitTimestamp = DateTimeOffset.Parse("2019-11-04T03:30:00Z"),
                            CommitId = OrderedCommitIds[10],
                        },
                        new CatalogLeafItem
                        {
                            Url = "https://example/catalog/item11.json",
                            PackageId = "NuGet.Commands",
                            PackageVersion = "5.3.0",
                            Type = CatalogLeafType.PackageDetails,
                            CommitTimestamp = DateTimeOffset.Parse("2019-11-04T04:00:00Z"),
                            CommitId = OrderedCommitIds[11],
                        },
                    },
                    Context = PageContext,
                });

                Target = new Mock<TestableCommitCollector>(
                    Index,
                    TelemetryService.Object,
                    (Func<HttpMessageHandler>)(() => new InMemoryHttpHandler(Responses)),
                    TimeSpan.FromSeconds(30),
                    HttpRetryStrategy.Object)
                {
                    CallBase = true,
                };
            }

            public Uri Index { get; }
            public Mock<ITelemetryService> TelemetryService { get; }
            public Dictionary<string, string> Responses { get; }
            public Mock<IHttpRetryStrategy> HttpRetryStrategy { get; }
            public MemoryCursor Front { get; }
            public MemoryCursor Back { get; }
            public CancellationToken Token { get; }
            public Mock<TestableCommitCollector> Target { get; }

            public CatalogPageContext PageContext => new CatalogPageContext
            {
                Vocab = "http://schema.nuget.org/catalog#",
                NuGet = "http://schema.nuget.org/schema#",
            };

            public List<string> OrderedCommitIds { get; }

            public string Serialize<T>(T obj)
            {
                var settings = NuGetJsonSerialization.Settings;
                settings.Formatting = Formatting.Indented;
                return JsonConvert.SerializeObject(obj, settings);
            }
        }

        public class TestableCommitCollector : CommitCollector
        {
            public TestableCommitCollector(
                Uri index,
                ITelemetryService telemetryService,
                Func<HttpMessageHandler> handlerFunc,
                TimeSpan? httpClientTimeout,
                IHttpRetryStrategy httpRetryStrategy)
                : base(index, telemetryService, handlerFunc, httpClientTimeout, httpRetryStrategy)
            {
            }

            public List<IEnumerable<CatalogCommitItem>> Batches { get; } = new List<IEnumerable<CatalogCommitItem>>();

            protected override Task<bool> OnProcessBatchAsync(
                CollectorHttpClient client,
                IEnumerable<CatalogCommitItem> items,
                JToken context,
                DateTime commitTimeStamp,
                bool isLastBatch,
                CancellationToken cancellationToken)
            {
                Batches.Add(items);

                return Task.FromResult(true);
            }
        }
    }
}
