// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Protocol.Catalog;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGet.Services.Entities;
using NuGet.Services.Metadata.Catalog;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    public class NewPackageRegistrationFromDbProducerFacts
    {
        public class ProduceWorkAsync
        {
            private readonly Mock<IEntitiesContextFactory> _entitiesContextFactory;
            private readonly Mock<IEntitiesContext> _entitiesContext;
            private readonly Mock<IOptionsSnapshot<Db2AzureSearchConfiguration>> _options;
            private readonly Mock<IOptionsSnapshot<Db2AzureSearchDevelopmentConfiguration>> _developmentOptions;
            private readonly Db2AzureSearchConfiguration _config;
            private readonly Db2AzureSearchDevelopmentConfiguration _developmentConfig;
            private readonly RecordingLogger<NewPackageRegistrationFromDbProducer> _logger;
            private readonly DbSet<PackageRegistration> _packageRegistrations;
            private readonly DbSet<Package> _packages;
            private readonly ConcurrentBag<NewPackageRegistration> _work;
            private readonly CancellationToken _token;
            private readonly NewPackageRegistrationFromDbProducer _target;
            private readonly Mock<IAuxiliaryFileClient> _auxiliaryFileClient;
            private readonly Mock<IDownloadsV1JsonClient> _downloadsV1JsonClient;
            private readonly Mock<IDatabaseAuxiliaryDataFetcher> _databaseFetcher;
            private readonly Mock<IDownloadTransferrer> _downloadTransferrer;
            private readonly Mock<IFeatureFlagService> _featureFlags;
            private readonly Mock<ICatalogClient> _catalogClient;
            private readonly DownloadData _downloads;
            private readonly PopularityTransferData _popularityTransfers;
            private readonly SortedDictionary<string, long> _transferChanges;
            private HashSet<string> _excludedPackages;

            public ProduceWorkAsync(ITestOutputHelper output)
            {
                _entitiesContextFactory = new Mock<IEntitiesContextFactory>();
                _entitiesContext = new Mock<IEntitiesContext>();
                _options = new Mock<IOptionsSnapshot<Db2AzureSearchConfiguration>>();
                _config = new Db2AzureSearchConfiguration
                {
                    DatabaseBatchSize = 2,
                    EnablePopularityTransfers = true,
                };
                _developmentOptions = new Mock<IOptionsSnapshot<Db2AzureSearchDevelopmentConfiguration>>();
                _developmentConfig =new Db2AzureSearchDevelopmentConfiguration();
                _logger = output.GetLogger<NewPackageRegistrationFromDbProducer>();
                _packageRegistrations = DbSetMockFactory.Create<PackageRegistration>();
                _packages = DbSetMockFactory.Create<Package>();
                _work = new ConcurrentBag<NewPackageRegistration>();
                _token = CancellationToken.None;

                _auxiliaryFileClient = new Mock<IAuxiliaryFileClient>();
                _excludedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _auxiliaryFileClient
                    .Setup(x => x.LoadExcludedPackagesAsync())
                    .ReturnsAsync(() => _excludedPackages);
                _downloadsV1JsonClient = new Mock<IDownloadsV1JsonClient>();
                _downloads = new DownloadData();
                _downloadsV1JsonClient
                    .Setup(x => x.ReadAsync())
                    .ReturnsAsync(() => _downloads);

                _popularityTransfers = new PopularityTransferData();
                _databaseFetcher = new Mock<IDatabaseAuxiliaryDataFetcher>();
                _databaseFetcher
                    .Setup(x => x.GetPopularityTransfersAsync())
                    .ReturnsAsync(() => _popularityTransfers);

                _downloadTransferrer = new Mock<IDownloadTransferrer>();
                _transferChanges = new SortedDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                _downloadTransferrer
                    .Setup(x => x.InitializeDownloadTransfers(
                        It.IsAny<DownloadData>(),
                        It.IsAny<PopularityTransferData>()))
                    .Returns(_transferChanges);

                _featureFlags = new Mock<IFeatureFlagService>();
                _featureFlags
                    .Setup(x => x.IsPopularityTransferEnabled())
                    .Returns(true);

                _catalogClient = new Mock<ICatalogClient>();

                _entitiesContextFactory
                   .Setup(x => x.CreateAsync(It.IsAny<bool>()))
                   .ReturnsAsync(() => _entitiesContext.Object);
                _entitiesContext
                    .Setup(x => x.Set<PackageRegistration>())
                    .Returns(() => _packageRegistrations);
                _entitiesContext
                    .Setup(x => x.Set<Package>())
                    .Returns(() => _packages);
                _options
                    .Setup(x => x.Value)
                    .Returns(() => _config);
                _developmentOptions
                    .Setup(x => x.Value)
                    .Returns(() => _developmentConfig);

                _target = new NewPackageRegistrationFromDbProducer(
                    _entitiesContextFactory.Object,
                    _auxiliaryFileClient.Object,
                    _downloadsV1JsonClient.Object,
                    _databaseFetcher.Object,
                    _downloadTransferrer.Object,
                    _featureFlags.Object,
                    _catalogClient.Object,
                    _options.Object,
                    _developmentOptions.Object,
                    _logger);
            }

            [Fact]
            public async Task AllowsNoWork()
            {
                await _target.ProduceWorkAsync(_work, _token);

                Assert.Empty(_work);
                _entitiesContextFactory.Verify(x => x.CreateAsync(true), Times.Once);
                _entitiesContext.Verify(x => x.Set<PackageRegistration>(), Times.Once);
                _entitiesContext.Verify(x => x.Set<Package>(), Times.Never);
            }

            [Fact]
            public async Task DoesNotCountUnavailablePackages()
            {
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 1,
                    Id = "A",
                    Packages = new[]
                    {
                        new Package { Version = "1.0.0", PackageStatusKey = PackageStatus.Deleted },
                        new Package { Version = "2.0.0" },
                    }
                });
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 2,
                    Id = "B",
                    Packages = new[]
                    {
                        new Package { Version = "3.0.0" },
                    }
                });
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 3,
                    Id = "C",
                    Packages = new[]
                    {
                        new Package { Version = "4.0.0" },
                    }
                });
                InitializePackagesFromPackageRegistrations();

                await _target.ProduceWorkAsync(_work, _token);

                Assert.Equal(3, _work.Count);
                Assert.Contains(
                    "Fetching packages with package registration key >= 1 and <= 2 (~2 packages).",
                    _logger.Messages);
                Assert.Contains(
                    "Fetching packages with package registration key >= 3 (~1 packages).",
                    _logger.Messages);
            }

            [Fact]
            public async Task FillsGapsInRanges()
            {
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 2,
                    Id = "A",
                    Packages = new[]
                    {
                        new Package { Version = "1.0.0" },
                    }
                });
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 10,
                    Id = "B",
                    Packages = new[]
                    {
                        new Package { Version = "2.0.0" },
                    }
                });
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 1000,
                    Id = "C",
                    Packages = new[]
                    {
                        new Package { Version = "3.0.0" },
                    }
                });
                InitializePackagesFromPackageRegistrations();

                await _target.ProduceWorkAsync(_work, _token);

                Assert.Equal(3, _work.Count);
                Assert.Contains(
                    "Fetching packages with package registration key >= 1 and <= 999 (~2 packages).",
                    _logger.Messages);
                Assert.Contains(
                    "Fetching packages with package registration key >= 1000 (~1 packages).",
                    _logger.Messages);
            }

            [Fact]
            public async Task ProducesWorkPerPackageRegistration()
            {
                _config.DatabaseBatchSize = 4;
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 1,
                    Id = "A",
                    Owners = new[] { new User { Username = "OwnerA" } },
                    Packages = new[]
                    {
                        new Package { Version = "1.0.0" },
                        new Package { Version = "2.0.0" },
                    },
                });
                _downloads.SetDownloadCount("A", "1.0.0", 23);
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 2,
                    Id = "B",
                    Owners = new[] { new User { Username = "OwnerB" } },
                    Packages = new[]
                    {
                        new Package { Version = "3.0.0" },
                    },
                });
                _downloads.SetDownloadCount("B", "3.0.0", 24);
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 3,
                    Id = "C",
                    Owners = new[] { new User { Username = "OwnerC" }, new User { Username = "OwnerD" } },
                    Packages = new[]
                    {
                        new Package { Version = "4.0.0" },
                    },
                });
                _downloads.SetDownloadCount("C", "4.0.0", 25);
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 4,
                    Id = "D",
                    DownloadCount = 26,
                    Owners = new[] { new User { Username = "OwnerE" } },
                    Packages = new Package[0],
                });
                _downloads.SetDownloadCount("D", "5.0.0", 26);
                InitializePackagesFromPackageRegistrations();

                await _target.ProduceWorkAsync(_work, _token);

                var work = _work.Reverse().ToList();
                Assert.Equal(4, work.Count);

                Assert.Equal("A", work[0].PackageId);
                Assert.Equal("1.0.0", work[0].Packages[0].Version);
                Assert.Equal("2.0.0", work[0].Packages[1].Version);
                Assert.Equal(new[] { "OwnerA" }, work[0].Owners);
                Assert.Equal(23, work[0].TotalDownloadCount);

                Assert.Equal("B", work[1].PackageId);
                Assert.Equal("3.0.0", work[1].Packages[0].Version);
                Assert.Equal(new[] { "OwnerB" }, work[1].Owners);
                Assert.Equal(24, work[1].TotalDownloadCount);

                Assert.Equal("C", work[2].PackageId);
                Assert.Equal("4.0.0", work[2].Packages[0].Version);
                Assert.Equal(new[] { "OwnerC", "OwnerD" }, work[2].Owners);
                Assert.Equal(25, work[2].TotalDownloadCount);

                Assert.Equal("D", work[3].PackageId);
                Assert.Empty(work[3].Packages);
                Assert.Equal(new[] { "OwnerE" }, work[3].Owners);
                Assert.Equal(26, work[3].TotalDownloadCount);
            }

            [Fact]
            public async Task DefaultsPackageDownloads()
            {
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 1,
                    Id = "HasDownloads",
                    Packages = new[]
                    {
                        new Package { Version = "1.0.0" },
                    },
                });
                _downloads.SetDownloadCount("HasDownloads", "1.0.0", 100);
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 2,
                    Id = "NoDownloads",
                    Packages = new[]
                    {
                        new Package { Version = "1.0.0" },
                    },
                });

                InitializePackagesFromPackageRegistrations();

                var result = await _target.ProduceWorkAsync(_work, _token);

                // Documents should have overriden downloads.
                var work = _work.Reverse().ToList();
                Assert.Equal(2, work.Count);

                Assert.Equal("HasDownloads", work[0].PackageId);
                Assert.Equal("1.0.0", work[0].Packages[0].Version);
                Assert.Equal(100, work[0].TotalDownloadCount);
                Assert.Equal("NoDownloads", work[1].PackageId);
                Assert.Equal("1.0.0", work[1].Packages[0].Version);
                Assert.Equal(0, work[1].TotalDownloadCount);

                // Downloads auxiliary file should have original downloads.
                Assert.Equal(100, result.Downloads["HasDownloads"]["1.0.0"]);
                Assert.False(result.Downloads.ContainsKey("NoDownloads"));
            }

            [Fact]
            public async Task RetrievesAndUsesExclusionList()
            {
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 1,
                    Id = "A",
                    Owners = new User[0],
                    Packages = new[]
                    {
                        new Package { Version = "1.0.0" },
                        new Package { Version = "2.0.0" },
                    },
                });
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 2,
                    Id = "B",
                    Owners = new User[0],
                    Packages = new[]
                    {
                        new Package { Version = "3.0.0" },
                    },
                });
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 3,
                    Id = "C",
                    Owners = new User[0],
                    Packages = new[]
                    {
                        new Package { Version = "4.0.0" },
                    },
                });
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 4,
                    Id = "D",
                    Owners = new User[0],
                    Packages = new Package[0],
                });

                InitializePackagesFromPackageRegistrations();

                _excludedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "A", "C" };

                await _target.ProduceWorkAsync(_work, _token);

                var work = _work.Reverse().ToList();
                Assert.Equal(4, work.Count);
                for (int i = 0; i < work.Count; i++)
                {
                    var shouldBeExcluded = _excludedPackages.Contains(work[i].PackageId, StringComparer.OrdinalIgnoreCase);
                    Assert.Equal(shouldBeExcluded, work[i].IsExcludedByDefault);
                }
            }

            [Fact]
            public async Task SkipsUnwantedPackages()
            {
                _developmentConfig.SkipPackagePrefixes = new List<string> { "Foo" };

                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 1,
                    Id = "FOO.Bar",
                    Owners = new User[0],
                    Packages = new[]
                     {
                        new Package { Version = "1.0.0" },
                    },
                });
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 2,
                    Id = "foo.Buzz",
                    Owners = new User[0],
                    Packages = new[]
                    {
                        new Package { Version = "2.0.0" },
                    },
                });
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 3,
                    Id = "Hello.World",
                    Owners = new User[0],
                    Packages = new[]
                    {
                        new Package { Version = "3.0.0" },
                    },
                });

                InitializePackagesFromPackageRegistrations();

                await _target.ProduceWorkAsync(_work, _token);

                var newRegistration = Assert.Single(_work);
                Assert.Equal("Hello.World", newRegistration.PackageId);
            }

            [Fact]
            public async Task ReturnsInitialAuxiliaryData()
            {
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 1,
                    Id = "A",
                    Owners = new[] { new User { Username = "OwnerA" } },
                    Packages = new[]
                    {
                        new Package { Version = "1.0.0" },
                        new Package { Version = "2.0.0" },
                    },
                    IsVerified = true,
                });

                var output = await _target.ProduceWorkAsync(_work, _token);

                Assert.Same(_downloads, output.Downloads);
                Assert.Same(_excludedPackages, output.ExcludedPackages);
                Assert.Same(_popularityTransfers, output.PopularityTransfers);
                Assert.NotNull(output.VerifiedPackages);
                Assert.Contains("A", output.VerifiedPackages);
                Assert.NotNull(output.Owners);
                Assert.Contains("A", output.Owners.Keys);
                Assert.Equal(new[] { "OwnerA" }, output.Owners["A"].ToArray());

                _auxiliaryFileClient.Verify(x => x.LoadExcludedPackagesAsync(), Times.Once);
            }

            [Fact]
            public async Task ThrowsWhenExcludedPackagesIsMissing()
            {
                _auxiliaryFileClient
                    .Setup(x => x.LoadExcludedPackagesAsync())
                    .ThrowsAsync(new CloudBlobNotFoundException(null));

                await Assert.ThrowsAsync<CloudBlobNotFoundException>(async () => await _target.ProduceWorkAsync(_work, _token));
            }

            [Fact]
            public async Task AppliesDownloadTransfers()
            {
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 1,
                    Id = "A",
                    Packages = new[]
                    {
                        new Package { Version = "1.0.0" },
                        new Package { Version = "2.0.0" },
                    },
                });
                _downloads.SetDownloadCount("A", "1.0.0", 12);
                _downloads.SetDownloadCount("A", "2.0.0", 23);
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 2,
                    Id = "B",
                    Packages = new[]
                    {
                        new Package { Version = "3.0.0" },
                        new Package { Version = "4.0.0" },
                    },
                });
                _downloads.SetDownloadCount("B", "3.0.0", 5);
                _downloads.SetDownloadCount("B", "4.0.0", 4);
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 3,
                    Id = "C",
                    Packages = new[]
                    {
                        new Package { Version = "5.0.0" },
                        new Package { Version = "6.0.0" },
                    },
                });
                _downloads.SetDownloadCount("C", "5.0.0", 2);
                _downloads.SetDownloadCount("C", "6.0.0", 3);

                InitializePackagesFromPackageRegistrations();

                // Transfer changes should be applied to the package registrations.
                _transferChanges["A"] = 55;
                _transferChanges["b"] = 66;
                _transferChanges["C"] = 123;

                var result = await _target.ProduceWorkAsync(_work, _token);
                _databaseFetcher.Verify(x => x.GetPopularityTransfersAsync(), Times.Once);

                _downloadTransferrer
                    .Verify(
                        x => x.InitializeDownloadTransfers(_downloads, _popularityTransfers),
                        Times.Once);

                // Documents should have overriden downloads.
                var work = _work.Reverse().ToList();
                Assert.Equal(3, work.Count);

                Assert.Equal("A", work[0].PackageId);
                Assert.Equal("1.0.0", work[0].Packages[0].Version);
                Assert.Equal("2.0.0", work[0].Packages[1].Version);
                Assert.Equal(55, work[0].TotalDownloadCount);

                Assert.Equal("B", work[1].PackageId);
                Assert.Equal("3.0.0", work[1].Packages[0].Version);
                Assert.Equal("4.0.0", work[1].Packages[1].Version);
                Assert.Equal(66, work[1].TotalDownloadCount);

                Assert.Equal("C", work[2].PackageId);
                Assert.Equal("5.0.0", work[2].Packages[0].Version);
                Assert.Equal("6.0.0", work[2].Packages[1].Version);
                Assert.Equal(123, work[2].TotalDownloadCount);

                // Downloads auxiliary file should have original downloads.
                Assert.Equal(12, result.Downloads["A"]["1.0.0"]);
                Assert.Equal(23, result.Downloads["A"]["2.0.0"]);
                Assert.Equal(5, result.Downloads["B"]["3.0.0"]);
                Assert.Equal(4, result.Downloads["B"]["4.0.0"]);
                Assert.Equal(2, result.Downloads["C"]["5.0.0"]);
                Assert.Equal(3, result.Downloads["C"]["6.0.0"]);
            }

            [Fact]
            public async Task ConfigDisablesPopularityTransfers()
            {
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 1,
                    Id = "A",
                    Packages = new[]
                    {
                        new Package { Version = "1.0.0" },
                    },
                });
                _downloads.SetDownloadCount("A", "1.0.0", 100);
                _popularityTransfers.AddTransfer("A", "A");

                InitializePackagesFromPackageRegistrations();

                _config.EnablePopularityTransfers = false;

                var result = await _target.ProduceWorkAsync(_work, _token);

                // The popularity transfers should not be loaded from the database.
                _databaseFetcher
                    .Verify(
                        x => x.GetPopularityTransfersAsync(),
                        Times.Never);

                // Popularity transfers should not be passed to the download transferrer.
                _downloadTransferrer
                    .Verify(
                        x => x.InitializeDownloadTransfers(
                            _downloads,
                            It.Is<PopularityTransferData>(data => data.Count == 0)),
                        Times.Once);

                // There should be no popularity transfers.
                Assert.Empty(result.PopularityTransfers);
            }

            [Fact]
            public async Task FlagDisablesPopularityTransfers()
            {
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 1,
                    Id = "A",
                    Packages = new[]
                    {
                        new Package { Version = "1.0.0" },
                    },
                });
                _downloads.SetDownloadCount("A", "1.0.0", 100);
                _popularityTransfers.AddTransfer("A", "A");

                InitializePackagesFromPackageRegistrations();

                _featureFlags
                    .Setup(x => x.IsPopularityTransferEnabled())
                    .Returns(false);

                var result = await _target.ProduceWorkAsync(_work, _token);

                // The popularity transfers should not be loaded from the database.
                _databaseFetcher
                    .Verify(
                        x => x.GetPopularityTransfersAsync(),
                        Times.Never);

                // Popularity transfers should not be passed to the download transferrer.
                _downloadTransferrer
                    .Verify(
                        x => x.InitializeDownloadTransfers(
                            _downloads,
                            It.Is<PopularityTransferData>(data => data.Count == 0)),
                        Times.Once);

                // There should be no popularity transfers.
                Assert.Empty(result.PopularityTransfers);
            }

            [Fact]
            public async Task IgnoresDownloadTransfersForNonexistentPackages()
            {
                _packageRegistrations.Add(new PackageRegistration
                {
                    Key = 1,
                    Id = "A",
                    Packages = new[]
                    {
                        new Package { Version = "1.0.0" },
                    },
                });
                _downloads.SetDownloadCount("A", "1.0.0", 100);

                InitializePackagesFromPackageRegistrations();

                // Transfer changes should be applied to the package registrations.
                // Transfer changes for packages that do not exist should be ignored.
                _transferChanges["PackageDoesNotExist"] = 123;

                var result = await _target.ProduceWorkAsync(_work, _token);

                // Documents should have overriden downloads.
                var work = _work.ToList();
                Assert.Single(work);

                Assert.Equal("A", work[0].PackageId);
                Assert.Equal("1.0.0", work[0].Packages[0].Version);
                Assert.Equal(100, work[0].TotalDownloadCount);

                // Downloads auxiliary file should have original downloads.
                Assert.Equal(100, result.Downloads["A"]["1.0.0"]);
            }

            private void InitializePackagesFromPackageRegistrations()
            {
                foreach (var pr in _packageRegistrations)
                {
                    foreach (var package in pr.Packages)
                    {
                        package.PackageRegistration = pr;
                        package.PackageRegistrationKey = pr.Key;

                        _packages.Add(package);
                    }
                }
            }
        }
    }
}
