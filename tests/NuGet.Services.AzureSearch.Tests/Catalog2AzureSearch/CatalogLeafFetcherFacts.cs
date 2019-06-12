// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Protocol.Catalog;
using NuGet.Protocol.Registration;
using NuGet.Services.AzureSearch.Support;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch
{
    public class CatalogLeafFetcherFacts
    {
        public class GetLatestLeavesAsync : BaseFacts
        {
            public GetLatestLeavesAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task ConsidersAllVersionsUnavailableIfIndexIsMissing()
            {
                _registrationClient
                    .Setup(x => x.GetIndexOrNullAsync(It.IsAny<string>()))
                    .ReturnsAsync((RegistrationIndex)null);

                var latest = await _target.GetLatestLeavesAsync(PackageId, _versions);

                Assert.Equal(_eachVersion, latest.Unavailable.OrderBy(x => x).ToArray());
                Assert.Empty(latest.Available);
                _registrationClient.Verify(
                    x => x.GetIndexOrNullAsync(It.IsAny<string>()), Times.Once);
                _registrationClient.Verify(
                    x => x.GetIndexOrNullAsync("https://example/v3-registration/nuget.versioning/index.json"), Times.Once);
                _registrationClient.Verify(
                    x => x.GetPageAsync(It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task ConsidersVersionOutsideOfRangeAsMissing()
            {
                var index = new RegistrationIndex
                {
                    Items = new List<RegistrationPage>
                    {
                        new RegistrationPage
                        {
                            Lower = "10.0.0",
                            Upper = "10.0.0",
                            Url = "http://example/page",
                            Items = new List<RegistrationLeafItem>
                            {
                                new RegistrationLeafItem
                                {
                                    CatalogEntry = new RegistrationCatalogEntry
                                    {
                                        Version = "10.0.0",
                                    },
                                },
                            },
                        },
                    },
                };
                _registrationClient
                    .Setup(x => x.GetIndexOrNullAsync(It.IsAny<string>()))
                    .ReturnsAsync(index);

                var latest = await _target.GetLatestLeavesAsync(PackageId, _versions);

                Assert.Equal(_eachVersion, latest.Unavailable.OrderBy(x => x).ToArray());
                Assert.Empty(latest.Available);
                _registrationClient.Verify(
                    x => x.GetIndexOrNullAsync(It.IsAny<string>()), Times.Once);
                _registrationClient.Verify(
                    x => x.GetIndexOrNullAsync("https://example/v3-registration/nuget.versioning/index.json"), Times.Once);
                _registrationClient.Verify(
                    x => x.GetPageAsync(It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task ConsidersMissingVersionAsUnavailable()
            {
                var index = new RegistrationIndex
                {
                    Items = new List<RegistrationPage>
                    {
                        new RegistrationPage
                        {
                            Lower = "0.0.0",
                            Upper = "10.0.0",
                            Url = "http://example/page",
                            Items = new List<RegistrationLeafItem>
                            {
                                new RegistrationLeafItem
                                {
                                    CatalogEntry = new RegistrationCatalogEntry
                                    {
                                        Version = "10.0.0",
                                    },
                                },
                            },
                        },
                    },
                };
                _registrationClient
                    .Setup(x => x.GetIndexOrNullAsync(It.IsAny<string>()))
                    .ReturnsAsync(index);

                var latest = await _target.GetLatestLeavesAsync(PackageId, _versions);

                Assert.Equal(_eachVersion, latest.Unavailable.OrderBy(x => x).ToArray());
                Assert.Empty(latest.Available);
                _registrationClient.Verify(
                    x => x.GetIndexOrNullAsync(It.IsAny<string>()), Times.Once);
                _registrationClient.Verify(
                    x => x.GetIndexOrNullAsync("https://example/v3-registration/nuget.versioning/index.json"), Times.Once);
                _registrationClient.Verify(
                    x => x.GetPageAsync(It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task ContinuesWhenVersionIsFoundToBeUnlistedOneVersionList()
            {
                var versions = new List<IReadOnlyList<NuGetVersion>>
                {
                    new List<NuGetVersion>
                    {
                        Parse("1.0.0"),
                        Parse("2.0.0"),
                        Parse("3.0.0"),
                    },
                };
                var details1 = new PackageDetailsCatalogLeaf { Listed = true };
                var details2 = new PackageDetailsCatalogLeaf { Listed = true };
                var details3 = new PackageDetailsCatalogLeaf { Listed = false };
                var index = new RegistrationIndex
                {
                    Items = new List<RegistrationPage>
                    {
                        new RegistrationPage
                        {
                            Lower = "1.0.0",
                            Upper = "3.0.0",
                            Url = "https://example/page",
                            Items = new List<RegistrationLeafItem>
                            {
                                new RegistrationLeafItem
                                {
                                    CatalogEntry = new RegistrationCatalogEntry
                                    {
                                        Url = "https://example/1.0.0",
                                        Version = "1.0.0",
                                        Listed = true,
                                    },
                                },
                                new RegistrationLeafItem
                                {
                                    CatalogEntry = new RegistrationCatalogEntry
                                    {
                                        Url = "https://example/2.0.0",
                                        Version = "2.0.0",
                                        Listed = true,
                                    },
                                },
                                new RegistrationLeafItem
                                {
                                    CatalogEntry = new RegistrationCatalogEntry
                                    {
                                        Url = "https://example/3.0.0",
                                        Version = "3.0.0",
                                        Listed = false,
                                    },
                                },
                            },
                        },
                    },
                };
                _catalogClient
                    .Setup(x => x.GetPackageDetailsLeafAsync("https://example/1.0.0"))
                    .ReturnsAsync(details1);
                _catalogClient
                    .Setup(x => x.GetPackageDetailsLeafAsync("https://example/2.0.0"))
                    .ReturnsAsync(details2);
                _catalogClient
                    .Setup(x => x.GetPackageDetailsLeafAsync("https://example/3.0.0"))
                    .ReturnsAsync(details3);
                _registrationClient
                    .Setup(x => x.GetIndexOrNullAsync(It.IsAny<string>()))
                    .ReturnsAsync(index);

                var latest = await _target.GetLatestLeavesAsync(PackageId, versions);

                Assert.Empty(latest.Unavailable);
                Assert.Equal(
                    new[] { Parse("2.0.0"), Parse("3.0.0") },
                    latest.Available.Keys.OrderBy(x => x).ToArray());
                Assert.Same(details2, latest.Available[Parse("2.0.0")]);
                Assert.Same(details3, latest.Available[Parse("3.0.0")]);
                _registrationClient.Verify(
                    x => x.GetIndexOrNullAsync(It.IsAny<string>()), Times.Once);
                _registrationClient.Verify(
                    x => x.GetIndexOrNullAsync("https://example/v3-registration/nuget.versioning/index.json"), Times.Once);
                _registrationClient.Verify(
                    x => x.GetPageAsync(It.IsAny<string>()), Times.Never);
                _catalogClient.Verify(
                    x => x.GetPackageDetailsLeafAsync(It.IsAny<string>()),
                    Times.Exactly(2));
                _catalogClient.Verify(
                    x => x.GetPackageDetailsLeafAsync("https://example/1.0.0"),
                    Times.Never);
            }

            [Fact]
            public async Task ContinuesWhenVersionIsFoundToBeUnlistedDifferentVersionLists()
            {
                var versions = new List<IReadOnlyList<NuGetVersion>>
                {
                    new List<NuGetVersion>
                    {
                        Parse("1.0.0+git"),
                        Parse("2.0.0-alpha"),
                        Parse("3.0.0"),
                    },
                    new List<NuGetVersion>
                    {
                        Parse("2.0.0-alpha"),
                        Parse("3.0.0"),
                    },
                };
                var details1 = new PackageDetailsCatalogLeaf { Listed = true };
                var details2 = new PackageDetailsCatalogLeaf { Listed = true };
                var details3 = new PackageDetailsCatalogLeaf { Listed = false };
                var index = new RegistrationIndex
                {
                    Items = new List<RegistrationPage>
                    {
                        new RegistrationPage
                        {
                            Lower = "1.0.0",
                            Upper = "3.0.0",
                            Url = "https://example/page",
                            Items = new List<RegistrationLeafItem>
                            {
                                new RegistrationLeafItem
                                {
                                    CatalogEntry = new RegistrationCatalogEntry
                                    {
                                        Url = "https://example/1.0.0",
                                        Version = "1.0.0+git",
                                        Listed = true,
                                    },
                                },
                                new RegistrationLeafItem
                                {
                                    CatalogEntry = new RegistrationCatalogEntry
                                    {
                                        Url = "https://example/2.0.0-alpha",
                                        Version = "2.0.0-alpha",
                                        Listed = true,
                                    },
                                },
                                new RegistrationLeafItem
                                {
                                    CatalogEntry = new RegistrationCatalogEntry
                                    {
                                        Url = "https://example/3.0.0",
                                        Version = "3.0.0",
                                        Listed = false,
                                    },
                                },
                            },
                        },
                    },
                };
                _catalogClient
                    .Setup(x => x.GetPackageDetailsLeafAsync("https://example/1.0.0"))
                    .ReturnsAsync(details1);
                _catalogClient
                    .Setup(x => x.GetPackageDetailsLeafAsync("https://example/2.0.0-alpha"))
                    .ReturnsAsync(details2);
                _catalogClient
                    .Setup(x => x.GetPackageDetailsLeafAsync("https://example/3.0.0"))
                    .ReturnsAsync(details3);
                _registrationClient
                    .Setup(x => x.GetIndexOrNullAsync(It.IsAny<string>()))
                    .ReturnsAsync(index);

                var latest = await _target.GetLatestLeavesAsync(PackageId, versions);

                Assert.Empty(latest.Unavailable);
                Assert.Equal(
                    new[] { Parse("2.0.0-alpha"), Parse("3.0.0") },
                    latest.Available.Keys.OrderBy(x => x).ToArray());
                Assert.Same(details2, latest.Available[Parse("2.0.0-alpha")]);
                Assert.Same(details3, latest.Available[Parse("3.0.0")]);
                _registrationClient.Verify(
                    x => x.GetIndexOrNullAsync(It.IsAny<string>()), Times.Once);
                _registrationClient.Verify(
                    x => x.GetIndexOrNullAsync("https://example/v3-registration/nuget.versioning/index.json"), Times.Once);
                _registrationClient.Verify(
                    x => x.GetPageAsync(It.IsAny<string>()), Times.Never);
                _catalogClient.Verify(
                    x => x.GetPackageDetailsLeafAsync(It.IsAny<string>()),
                    Times.Exactly(2));
                _catalogClient.Verify(
                    x => x.GetPackageDetailsLeafAsync("https://example/1.0.0"),
                    Times.Never);
            }

            [Fact]
            public async Task ReturnsAllProvidedVersionsIfUnlisted()
            {
                var versions = new List<IReadOnlyList<NuGetVersion>>
                {
                    new List<NuGetVersion>
                    {
                        Parse("1.0.0"),
                        Parse("2.0.0"),
                        Parse("3.0.0"),
                    },
                };
                var details1 = new PackageDetailsCatalogLeaf { Listed = false };
                var details2 = new PackageDetailsCatalogLeaf { Listed = false };
                var details3 = new PackageDetailsCatalogLeaf { Listed = false };
                var index = new RegistrationIndex
                {
                    Items = new List<RegistrationPage>
                    {
                        new RegistrationPage
                        {
                            Lower = "0.0.0",
                            Upper = "4.0.0",
                            Url = "https://example/page",
                            Items = new List<RegistrationLeafItem>
                            {
                                new RegistrationLeafItem
                                {
                                    CatalogEntry = new RegistrationCatalogEntry
                                    {
                                        Url = "https://example/0.0.0",
                                        Version = "0.0.0",
                                    },
                                },
                                new RegistrationLeafItem
                                {
                                    CatalogEntry = new RegistrationCatalogEntry
                                    {
                                        Url = "https://example/1.0.0",
                                        Version = "1.0.0",
                                    },
                                },
                                new RegistrationLeafItem
                                {
                                    CatalogEntry = new RegistrationCatalogEntry
                                    {
                                        Url = "https://example/2.0.0",
                                        Version = "2.0.0",
                                    },
                                },
                                new RegistrationLeafItem
                                {
                                    CatalogEntry = new RegistrationCatalogEntry
                                    {
                                        Url = "https://example/3.0.0",
                                        Version = "3.0.0",
                                    },
                                },
                                new RegistrationLeafItem
                                {
                                    CatalogEntry = new RegistrationCatalogEntry
                                    {
                                        Url = "https://example/4.0.0",
                                        Version = "4.0.0",
                                    },
                                },
                            },
                        },
                    },
                };
                _catalogClient
                    .Setup(x => x.GetPackageDetailsLeafAsync("https://example/1.0.0"))
                    .ReturnsAsync(details1);
                _catalogClient
                    .Setup(x => x.GetPackageDetailsLeafAsync("https://example/2.0.0"))
                    .ReturnsAsync(details2);
                _catalogClient
                    .Setup(x => x.GetPackageDetailsLeafAsync("https://example/3.0.0"))
                    .ReturnsAsync(details3);
                _registrationClient
                    .Setup(x => x.GetIndexOrNullAsync(It.IsAny<string>()))
                    .ReturnsAsync(index);

                var latest = await _target.GetLatestLeavesAsync(PackageId, versions);

                Assert.Empty(latest.Unavailable);
                Assert.Equal(
                    new[] { Parse("1.0.0"), Parse("2.0.0"), Parse("3.0.0") },
                    latest.Available.Keys.OrderBy(x => x).ToArray());
                Assert.Same(details1, latest.Available[Parse("1.0.0")]);
                Assert.Same(details2, latest.Available[Parse("2.0.0")]);
                Assert.Same(details3, latest.Available[Parse("3.0.0")]);
                _registrationClient.Verify(
                    x => x.GetIndexOrNullAsync(It.IsAny<string>()), Times.Once);
                _registrationClient.Verify(
                    x => x.GetIndexOrNullAsync("https://example/v3-registration/nuget.versioning/index.json"), Times.Once);
                _registrationClient.Verify(
                    x => x.GetPageAsync(It.IsAny<string>()), Times.Never);
                _catalogClient.Verify(
                    x => x.GetPackageDetailsLeafAsync(It.IsAny<string>()),
                    Times.Exactly(3));
                _catalogClient.Verify(
                    x => x.GetPackageDetailsLeafAsync("https://example/0.0.0"),
                    Times.Never);
                _catalogClient.Verify(
                    x => x.GetPackageDetailsLeafAsync("https://example/4.0.0"),
                    Times.Never);
            }

            [Fact]
            public async Task FetchesPageIfItemsAreNotInlined()
            {
                var versions = new List<IReadOnlyList<NuGetVersion>>
                {
                    new List<NuGetVersion>
                    {
                        Parse("1.0.0"),
                    },
                };
                var details1 = new PackageDetailsCatalogLeaf { Listed = true };
                var page = new RegistrationPage
                {
                    Items = new List<RegistrationLeafItem>
                    {
                        new RegistrationLeafItem
                        {
                            CatalogEntry = new RegistrationCatalogEntry
                            {
                                Url = "https://example/1.0.0",
                                Version = "1.0.0",
                            },
                        },
                    },
                };
                var index = new RegistrationIndex
                {
                    Items = new List<RegistrationPage>
                    {
                        new RegistrationPage
                        {
                            Lower = "1.0.0",
                            Upper = "1.0.0",
                            Url = "https://example/page",
                        },
                    },
                };
                _catalogClient
                    .Setup(x => x.GetPackageDetailsLeafAsync("https://example/1.0.0"))
                    .ReturnsAsync(details1);
                _registrationClient
                    .Setup(x => x.GetPageAsync("https://example/page"))
                    .ReturnsAsync(page);
                _registrationClient
                    .Setup(x => x.GetIndexOrNullAsync(It.IsAny<string>()))
                    .ReturnsAsync(index);

                var latest = await _target.GetLatestLeavesAsync(PackageId, versions);

                Assert.Empty(latest.Unavailable);
                Assert.Equal(
                    new[] { Parse("1.0.0") },
                    latest.Available.Keys.OrderBy(x => x).ToArray());
                Assert.Same(details1, latest.Available[Parse("1.0.0")]);
                _registrationClient.Verify(
                    x => x.GetIndexOrNullAsync(It.IsAny<string>()), Times.Once);
                _registrationClient.Verify(
                    x => x.GetIndexOrNullAsync("https://example/v3-registration/nuget.versioning/index.json"), Times.Once);
                _registrationClient.Verify(
                    x => x.GetPageAsync(It.IsAny<string>()), Times.Once);
                _registrationClient.Verify(
                    x => x.GetPageAsync("https://example/page"), Times.Once);
                _catalogClient.Verify(
                    x => x.GetPackageDetailsLeafAsync(It.IsAny<string>()),
                    Times.Once);
                _catalogClient.Verify(
                    x => x.GetPackageDetailsLeafAsync("https://example/1.0.0"),
                    Times.Once);
            }

            [Fact]
            public async Task FetchesLeavesOnlyOnce()
            {
                var details1 = new PackageDetailsCatalogLeaf { Listed = true };
                var details2 = new PackageDetailsCatalogLeaf { Listed = true };
                var details3 = new PackageDetailsCatalogLeaf { Listed = true };
                var details4 = new PackageDetailsCatalogLeaf { Listed = true };
                var index = new RegistrationIndex
                {
                    Items = new List<RegistrationPage>
                    {
                        new RegistrationPage
                        {
                            Lower = "0.0.0",
                            Upper = "10.0.0",
                            Url = "https://example/page",
                            Items = new List<RegistrationLeafItem>
                            {
                                new RegistrationLeafItem
                                {
                                    CatalogEntry = new RegistrationCatalogEntry
                                    {
                                        Url = "https://example/1",
                                        Version = "1.0.0",
                                        Listed = true,
                                    },
                                },
                                new RegistrationLeafItem
                                {
                                    CatalogEntry = new RegistrationCatalogEntry
                                    {
                                        Url = "https://example/2",
                                        Version = "2.0.0-alpha",
                                        Listed = true,
                                    },
                                },
                                new RegistrationLeafItem
                                {
                                    CatalogEntry = new RegistrationCatalogEntry
                                    {
                                        Url = "https://example/3",
                                        Version = "3.0.0+git",
                                        Listed = true,
                                    },
                                },
                                new RegistrationLeafItem
                                {
                                    CatalogEntry = new RegistrationCatalogEntry
                                    {
                                        Url = "https://example/4",
                                        Version = "4.0.0-beta.1",
                                        Listed = true,
                                    },
                                },
                            },
                        },
                    },
                };
                _catalogClient
                    .Setup(x => x.GetPackageDetailsLeafAsync("https://example/1"))
                    .ReturnsAsync(details1);
                _catalogClient
                    .Setup(x => x.GetPackageDetailsLeafAsync("https://example/2"))
                    .ReturnsAsync(details2);
                _catalogClient
                    .Setup(x => x.GetPackageDetailsLeafAsync("https://example/3"))
                    .ReturnsAsync(details3);
                _catalogClient
                    .Setup(x => x.GetPackageDetailsLeafAsync("https://example/4"))
                    .ReturnsAsync(details4);
                _registrationClient
                    .Setup(x => x.GetIndexOrNullAsync(It.IsAny<string>()))
                    .ReturnsAsync(index);

                var latest = await _target.GetLatestLeavesAsync(PackageId, _versions);

                Assert.Empty(latest.Unavailable);
                Assert.Equal(
                    _eachVersion,
                    latest.Available.Keys.OrderBy(x => x).ToArray());
                Assert.Same(details1, latest.Available[Parse("1.0.0")]);
                Assert.Same(details2, latest.Available[Parse("2.0.0-alpha")]);
                Assert.Same(details3, latest.Available[Parse("3.0.0")]);
                Assert.Same(details4, latest.Available[Parse("4.0.0-beta.1")]);
                _registrationClient.Verify(
                    x => x.GetIndexOrNullAsync(It.IsAny<string>()), Times.Once);
                _registrationClient.Verify(
                    x => x.GetIndexOrNullAsync("https://example/v3-registration/nuget.versioning/index.json"), Times.Once);
                _registrationClient.Verify(
                    x => x.GetPageAsync(It.IsAny<string>()), Times.Never);
                _catalogClient.Verify(
                    x => x.GetPackageDetailsLeafAsync(It.IsAny<string>()),
                    Times.Exactly(4));
                _catalogClient.Verify(
                    x => x.GetPackageDetailsLeafAsync("https://example/1"),
                    Times.Once);
                _catalogClient.Verify(
                    x => x.GetPackageDetailsLeafAsync("https://example/2"),
                    Times.Once);
                _catalogClient.Verify(
                    x => x.GetPackageDetailsLeafAsync("https://example/3"),
                    Times.Once);
                _catalogClient.Verify(
                    x => x.GetPackageDetailsLeafAsync("https://example/4"),
                    Times.Once);
            }
        }

        public abstract class BaseFacts
        {
            protected const string PackageId = "NuGet.Versioning";

            protected readonly Mock<IRegistrationClient> _registrationClient;
            protected readonly Mock<ICatalogClient> _catalogClient;
            protected readonly Mock<IOptionsSnapshot<Catalog2AzureSearchConfiguration>> _options;
            protected readonly Catalog2AzureSearchConfiguration _config;
            protected readonly Mock<IAzureSearchTelemetryService> _telemetryService;
            protected readonly RecordingLogger<CatalogLeafFetcher> _logger;
            protected readonly List<IReadOnlyList<NuGetVersion>> _versions;
            protected readonly NuGetVersion[] _eachVersion;
            protected readonly CatalogLeafFetcher _target;

            public BaseFacts(ITestOutputHelper output)
            {
                _registrationClient = new Mock<IRegistrationClient>();
                _catalogClient = new Mock<ICatalogClient>();
                _options = new Mock<IOptionsSnapshot<Catalog2AzureSearchConfiguration>>();
                _config = new Catalog2AzureSearchConfiguration
                {
                    MaxConcurrentBatches = 1,
                };
                _telemetryService = new Mock<IAzureSearchTelemetryService>();
                _logger = output.GetLogger<CatalogLeafFetcher>();

                _options.Setup(x => x.Value).Returns(() => _config);

                _config.RegistrationsBaseUrl = "https://example/v3-registration/";
                _versions = new List<IReadOnlyList<NuGetVersion>>
                {
                    new List<NuGetVersion>
                    {
                        Parse("1.0.0"),
                    },
                    new List<NuGetVersion>
                    {
                        Parse("1.0.0"),
                        Parse("2.0.0-alpha"),
                    },
                    new List<NuGetVersion>
                    {
                        Parse("1.0.0"),
                        Parse("3.0.0+git"),
                    },
                    new List<NuGetVersion>
                    {
                        Parse("1.0.0"),
                        Parse("2.0.0-alpha"),
                        Parse("3.0.0+git"),
                        Parse("4.0.0-beta.1"),
                    },
                };
                _eachVersion = new[]
                {
                    Parse("1.0.0"),
                    Parse("2.0.0-alpha"),
                    Parse("3.0.0+git"),
                    Parse("4.0.0-beta.1"),
                };

                _target = new CatalogLeafFetcher(
                    _registrationClient.Object,
                    _catalogClient.Object,
                    _options.Object,
                    _telemetryService.Object,
                    _logger);
            }

            protected NuGetVersion Parse(string input)
            {
                return NuGetVersion.Parse(input);
            }
        }
    }
}
