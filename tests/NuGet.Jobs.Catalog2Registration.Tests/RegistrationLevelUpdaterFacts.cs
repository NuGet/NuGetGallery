// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NuGet.Protocol.Registration;
using NuGet.Services;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Jobs.Catalog2Registration
{
    public class RegistrationLevelUpdaterFacts
    {
        public class UpdateAsync : Facts
        {
            public UpdateAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task SetsSponsorshipUrlsOnEachPrimaryHiveAndReplicas()
            {
                var urls = new List<string> { "https://github.com/sponsors/nuget" };

                await Target.UpdateAsync(Id, urls, CommitTimestamp);

                HiveStorage.Verify(
                    x => x.WriteIndexAsync(
                        HiveType.Legacy,
                        It.Is<IReadOnlyList<HiveType>>(r => r.Count == 1 && r[0] == HiveType.Gzipped),
                        Id,
                        It.IsAny<RegistrationIndex>()),
                    Times.Once);
                HiveStorage.Verify(
                    x => x.WriteIndexAsync(
                        HiveType.SemVer2,
                        It.Is<IReadOnlyList<HiveType>>(r => r.Count == 0),
                        Id,
                        It.IsAny<RegistrationIndex>()),
                    Times.Once);
                HiveStorage.Verify(
                    x => x.WriteIndexAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<RegistrationIndex>()),
                    Times.Exactly(2));

                Assert.Equal(2, WrittenIndexes.Count);
                Assert.All(WrittenIndexes, index => Assert.Equal(
                    urls,
                    Assert.IsType<List<string>>(index.Metadata[RegistrationIndex.SponsorshipUrlsMetadataKey])));
            }

            [Fact]
            public async Task StampsCommitOnEachWrittenIndex()
            {
                await Target.UpdateAsync(Id, new List<string> { "https://example/sponsor" }, CommitTimestamp);

                EntityBuilder.Verify(
                    x => x.UpdateCommit(It.IsAny<RegistrationIndex>(), It.Is<CatalogCommit>(c => c.Timestamp == CommitTimestamp)),
                    Times.Exactly(2));
            }

            [Fact]
            public async Task RemovesSponsorshipUrlsWhenNull()
            {
                SetupExistingSponsorshipMetadata();

                await Target.UpdateAsync(Id, sponsorshipUrls: null, CommitTimestamp);

                Assert.Equal(2, WrittenIndexes.Count);
                Assert.All(WrittenIndexes, index => Assert.Null(index.Metadata));
            }

            [Fact]
            public async Task RemovesSponsorshipUrlsWhenEmpty()
            {
                SetupExistingSponsorshipMetadata();

                await Target.UpdateAsync(Id, new List<string>(), CommitTimestamp);

                Assert.Equal(2, WrittenIndexes.Count);
                Assert.All(WrittenIndexes, index => Assert.Null(index.Metadata));
            }

            [Fact]
            public async Task SkipsHiveWhenIndexDoesNotExist()
            {
                HiveStorage
                    .Setup(x => x.ReadIndexOrNullAsync(HiveType.SemVer2, Id))
                    .ReturnsAsync((RegistrationIndex)null);

                await Target.UpdateAsync(Id, new List<string> { "https://example/sponsor" }, CommitTimestamp);

                HiveStorage.Verify(
                    x => x.WriteIndexAsync(HiveType.SemVer2, It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<RegistrationIndex>()),
                    Times.Never);
                HiveStorage.Verify(
                    x => x.WriteIndexAsync(HiveType.Legacy, It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<RegistrationIndex>()),
                    Times.Once);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public async Task ThrowsWhenIdIsNullOrEmpty(string id)
            {
                await Assert.ThrowsAsync<ArgumentException>(
                    () => Target.UpdateAsync(id, new List<string>(), CommitTimestamp));
            }
        }

        public abstract class Facts
        {
            public Facts(ITestOutputHelper output)
            {
                HiveStorage = new Mock<IHiveStorage>();
                EntityBuilder = new Mock<IEntityBuilder>();
                Logger = output.GetLogger<RegistrationLevelUpdater>();
                WrittenIndexes = new List<RegistrationIndex>();

                HiveStorage
                    .Setup(x => x.ReadIndexOrNullAsync(It.IsAny<HiveType>(), It.IsAny<string>()))
                    .ReturnsAsync(() => new RegistrationIndex());
                HiveStorage
                    .Setup(x => x.WriteIndexAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<RegistrationIndex>()))
                    .Returns(Task.CompletedTask)
                    .Callback<HiveType, IReadOnlyList<HiveType>, string, RegistrationIndex>((h, r, i, index) => WrittenIndexes.Add(index));

                Target = new RegistrationLevelUpdater(HiveStorage.Object, EntityBuilder.Object, Logger);
            }

            public Mock<IHiveStorage> HiveStorage { get; }
            public Mock<IEntityBuilder> EntityBuilder { get; }
            public RecordingLogger<RegistrationLevelUpdater> Logger { get; }
            public List<RegistrationIndex> WrittenIndexes { get; }
            public string Id { get; } = "NuGet.Versioning";
            public DateTimeOffset CommitTimestamp { get; } = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
            public RegistrationLevelUpdater Target { get; }

            protected void SetupExistingSponsorshipMetadata()
            {
                HiveStorage
                    .Setup(x => x.ReadIndexOrNullAsync(It.IsAny<HiveType>(), It.IsAny<string>()))
                    .ReturnsAsync(() => new RegistrationIndex
                    {
                        Metadata = new Dictionary<string, object>
                        {
                            { RegistrationIndex.SponsorshipUrlsMetadataKey, new List<string> { "https://example/sponsor" } },
                        },
                    });
            }
        }
    }
}
