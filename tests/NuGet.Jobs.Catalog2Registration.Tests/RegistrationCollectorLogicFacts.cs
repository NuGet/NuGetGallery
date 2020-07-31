// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Packaging.Core;
using NuGet.Protocol.Catalog;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.V3;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Jobs.Catalog2Registration
{
    public class RegistrationCollectorLogicFacts
    {
        public class CreateBatchesAsync : Facts
        {
            public CreateBatchesAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task SingleBatchWithAllItems()
            {
                var items = new[]
                {
                    new CatalogCommitItem(
                        uri: null,
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 1),
                        types: null,
                        typeUris: new List<Uri>(),
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0"))),
                    new CatalogCommitItem(
                        uri: null,
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 2),
                        types: null,
                        typeUris: new List<Uri>(),
                        packageIdentity: new PackageIdentity("NuGet.Frameworks", NuGetVersion.Parse("2.0.0"))),
                };

                var batches = await Target.CreateBatchesAsync(items);

                var batch = Assert.Single(batches);
                Assert.Equal(2, batch.Items.Count);
                Assert.Equal(new DateTime(2018, 1, 2), batch.CommitTimeStamp);
                Assert.Equal(items[0], batch.Items[0]);
                Assert.Equal(items[1], batch.Items[1]);
            }
        }

        public class OnProcessBatchAsync : Facts
        {
            public OnProcessBatchAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task DoesNotFetchLeavesForDeleteEntries()
            {
                var items = new[]
                {
                    new CatalogCommitItem(
                        uri: new Uri("https://example/0"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 1),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0"))),
                    new CatalogCommitItem(
                        uri: new Uri("https://example/1"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 2),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDelete },
                        packageIdentity: new PackageIdentity("NuGet.Frameworks", NuGetVersion.Parse("2.0.0"))),
                };

                await Target.OnProcessBatchAsync(items);

                CatalogClient.Verify(x => x.GetPackageDetailsLeafAsync("https://example/0"), Times.Once);
                CatalogClient.Verify(x => x.GetPackageDetailsLeafAsync(It.IsAny<string>()), Times.Exactly(1));
                CatalogClient.Verify(x => x.GetPackageDeleteLeafAsync(It.IsAny<string>()), Times.Never);

                RegistrationUpdater.Verify(
                    x => x.UpdateAsync(
                        "NuGet.Versioning",
                        It.Is<IReadOnlyList<CatalogCommitItem>>(
                            y => y.Count == 1),
                        It.Is<IReadOnlyDictionary<CatalogCommitItem, PackageDetailsCatalogLeaf>>(
                            y => y.Count == 1)),
                    Times.Once);
                RegistrationUpdater.Verify(
                    x => x.UpdateAsync(
                        "NuGet.Frameworks",
                        It.Is<IReadOnlyList<CatalogCommitItem>>(
                            y => y.Count == 1),
                        It.Is<IReadOnlyDictionary<CatalogCommitItem, PackageDetailsCatalogLeaf>>(
                            y => y.Count == 0)),
                    Times.Once);
            }

            [Fact]
            public async Task OperatesOnLatestPerPackageIdentityAndGroupsById()
            {
                var items = new[]
                {
                    new CatalogCommitItem(
                        uri: new Uri("https://example/0"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 1),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0"))),
                    new CatalogCommitItem(
                        uri: new Uri("https://example/1"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 2),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0"))),
                    new CatalogCommitItem(
                        uri: new Uri("https://example/2"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 2),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("2.0.0"))),
                    new CatalogCommitItem(
                        uri: new Uri("https://example/3"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 2),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Frameworks", NuGetVersion.Parse("1.0.0"))),
                };

                await Target.OnProcessBatchAsync(items);

                CatalogClient.Verify(x => x.GetPackageDetailsLeafAsync("https://example/1"), Times.Once);
                CatalogClient.Verify(x => x.GetPackageDetailsLeafAsync("https://example/2"), Times.Once);
                CatalogClient.Verify(x => x.GetPackageDetailsLeafAsync("https://example/3"), Times.Once);
                CatalogClient.Verify(x => x.GetPackageDetailsLeafAsync(It.IsAny<string>()), Times.Exactly(3));

                RegistrationUpdater.Verify(
                    x => x.UpdateAsync(
                        "NuGet.Versioning",
                        It.Is<IReadOnlyList<CatalogCommitItem>>(
                            y => y.Count == 2),
                        It.Is<IReadOnlyDictionary<CatalogCommitItem, PackageDetailsCatalogLeaf>>(
                            y => y.Count == 2)),
                    Times.Once);
                RegistrationUpdater.Verify(
                    x => x.UpdateAsync(
                        "NuGet.Frameworks",
                        It.Is<IReadOnlyList<CatalogCommitItem>>(
                            y => y.Count == 1),
                        It.Is<IReadOnlyDictionary<CatalogCommitItem, PackageDetailsCatalogLeaf>>(
                            y => y.Count == 1)),
                    Times.Once);
            }

            [Fact]
            public async Task RejectsMultipleLeavesForTheSamePackageAtTheSameTime()
            {
                var items = new[]
                {
                    new CatalogCommitItem(
                        uri: new Uri("https://example/0"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 1),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0"))),
                    new CatalogCommitItem(
                        uri: new Uri("https://example/1"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 1),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0"))),
                };

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => Target.OnProcessBatchAsync(items));

                Assert.Equal(
                    "There are multiple catalog leaves for a single package at one time.",
                    ex.Message);
                RegistrationUpdater.Verify(
                    x => x.UpdateAsync(
                        It.IsAny<string>(),
                        It.IsAny<IReadOnlyList<CatalogCommitItem>>(),
                        It.IsAny<IReadOnlyDictionary<CatalogCommitItem, PackageDetailsCatalogLeaf>>()),
                    Times.Never);
            }
        }

        public abstract class Facts
        {
            public Facts(ITestOutputHelper output)
            {
                CatalogClient = new Mock<ICatalogClient>();
                V3TelemetryService = new Mock<IV3TelemetryService>();
                CommitCollectorOptions = new Mock<IOptionsSnapshot<CommitCollectorConfiguration>>();
                RegistrationUpdater = new Mock<IRegistrationUpdater>();
                Options = new Mock<IOptionsSnapshot<Catalog2RegistrationConfiguration>>();

                CommitCollectorConfiguration = new CommitCollectorConfiguration { MaxConcurrentCatalogLeafDownloads = 1 };
                CommitCollectorOptions.Setup(x => x.Value).Returns(() => CommitCollectorConfiguration);
                Configuration = new Catalog2RegistrationConfiguration { MaxConcurrentIds = 1 };
                Options.Setup(x => x.Value).Returns(() => Configuration);

                Target = new RegistrationCollectorLogic(
                    new CommitCollectorUtility(
                        CatalogClient.Object,
                        V3TelemetryService.Object,
                        CommitCollectorOptions.Object,
                        output.GetLogger<CommitCollectorUtility>()),
                    RegistrationUpdater.Object,
                    Options.Object,
                    output.GetLogger<RegistrationCollectorLogic>());
            }

            public Mock<ICatalogClient> CatalogClient { get; }
            public Mock<IV3TelemetryService> V3TelemetryService { get; }
            public Mock<IOptionsSnapshot<CommitCollectorConfiguration>> CommitCollectorOptions { get; }
            public Mock<IRegistrationUpdater> RegistrationUpdater { get; }
            public Mock<IOptionsSnapshot<Catalog2RegistrationConfiguration>> Options { get; }
            public CommitCollectorConfiguration CommitCollectorConfiguration { get; }
            public Catalog2RegistrationConfiguration Configuration { get; }
            public RegistrationCollectorLogic Target { get; }
        }
    }
}
