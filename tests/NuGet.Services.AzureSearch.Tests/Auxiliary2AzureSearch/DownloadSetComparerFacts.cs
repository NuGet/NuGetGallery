﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.Auxiliary2AzureSearch
{
    public class DownloadSetComparerFacts
    {
        public class Compare : Facts
        {
            public Compare(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public void RejectsEmptyNewData()
            {
                var oldData = new DownloadData();
                oldData.SetDownloadCount(IdA, V1, 5);
                var newData = new DownloadData();

                var ex = Assert.Throws<InvalidOperationException>(() => Target.Compare(oldData, newData));
                Assert.Equal("The new data should not be empty.", ex.Message);
            }

            [Theory]
            [InlineData(9, false)]
            [InlineData(10, false)]
            [InlineData(11, true)]
            [InlineData(12, true)]
            public void DetectsTooManyDecreases(int decreases, bool tooMany)
            {
                Config.MaxDownloadCountDecreases = 10;

                var oldData = new DownloadData();
                oldData.SetDownloadCount($"NuGet.Frameworks", "1.0.0-alpha", 1);
                for (var i = 0; i < decreases; i++)
                {
                    oldData.SetDownloadCount($"NuGet.Versioning.{i}", "1.0.0-alpha", 1);
                }

                var newData = new DownloadData();
                newData.SetDownloadCount($"NuGet.Frameworks", "1.0.0-alpha", 1);

                if (tooMany)
                {
                    var ex = Assert.Throws<InvalidOperationException>(() => Target.Compare(oldData, newData));
                    Assert.Equal("Too many download count decreases are occurring.", ex.Message);
                    Assert.Contains($"There are {decreases} package versions with download count decreases.", Logger.Messages);
                }
                else
                {
                    var delta = Target.Compare(oldData, newData);
                    Assert.Equal(decreases, delta.Count);
                }
            }

            [Fact]
            public void DetectsNoChange()
            {
                var oldData = new DownloadData();
                oldData.SetDownloadCount(IdA, V1, 5);
                oldData.SetDownloadCount(IdB, V1, 1);
                var newData = new DownloadData();
                newData.SetDownloadCount(IdA, V1, 5);
                newData.SetDownloadCount(IdB, V1, 1);

                var delta = Target.Compare(oldData, newData);

                Assert.Empty(delta);
                VerifyDecreaseTelemetry(Times.Never());
            }

            [Fact]
            public void DetectsIncreaseDueToRealIncrease()
            {
                var oldData = new DownloadData();
                oldData.SetDownloadCount(IdA, V1, 5);
                oldData.SetDownloadCount(IdB, V1, 1);
                var newData = new DownloadData();
                newData.SetDownloadCount(IdA, V1, 7); // Increase
                newData.SetDownloadCount(IdB, V1, 1); // No change

                var delta = Target.Compare(oldData, newData);

                Assert.Equal(KeyValuePair.Create(IdA, 7L), Assert.Single(delta));
                VerifyDecreaseTelemetry(Times.Never());
            }

            [Fact]
            public void DetectsIncreaseDueToAddedVersion()
            {
                var oldData = new DownloadData();
                oldData.SetDownloadCount(IdA, V1, 5);
                var newData = new DownloadData();
                newData.SetDownloadCount(IdA, V1, 5); // No change
                newData.SetDownloadCount(IdA, V2, 2); // Added

                var delta = Target.Compare(oldData, newData);

                Assert.Equal(KeyValuePair.Create(IdA, 7L), Assert.Single(delta));
                VerifyDecreaseTelemetry(Times.Never());
            }

            [Fact]
            public void DetectsIncreaseDueToAddedId()
            {
                var oldData = new DownloadData();
                var newData = new DownloadData();
                newData.SetDownloadCount(IdA, V1, 5); // Added
                newData.SetDownloadCount(IdA, V2, 2); // Added

                var delta = Target.Compare(oldData, newData);

                Assert.Equal(KeyValuePair.Create(IdA, 7L), Assert.Single(delta));
                VerifyDecreaseTelemetry(Times.Never());
            }

            [Fact]
            public void DetectsDecreaseWhenTotalRemainsTheSame()
            {
                var oldData = new DownloadData();
                oldData.SetDownloadCount(IdA, V1, 7);
                oldData.SetDownloadCount(IdA, V2, 1);
                var newData = new DownloadData();
                newData.SetDownloadCount(IdA, V1, 5);
                newData.SetDownloadCount(IdA, V2, 3);

                var delta = Target.Compare(oldData, newData);

                Assert.Empty(delta);
                VerifyDecreaseTelemetry(Times.Once());
                TelemetryService.Verify(
                    x => x.TrackDownloadCountDecrease(
                        IdA,
                        V1,
                        true,
                        true,
                        7,
                        true,
                        true,
                        5),
                    Times.Once);
            }

            [Fact]
            public void DetectsDecreaseDueToRealDecrease()
            {
                var oldData = new DownloadData();
                oldData.SetDownloadCount(IdA, V1, 7);
                oldData.SetDownloadCount(IdA, V2, 1);
                var newData = new DownloadData();
                newData.SetDownloadCount(IdA, V1, 5);
                newData.SetDownloadCount(IdA, V2, 1);

                var delta = Target.Compare(oldData, newData);

                Assert.Equal(KeyValuePair.Create(IdA, 6L), Assert.Single(delta));
                VerifyDecreaseTelemetry(Times.Once());
                TelemetryService.Verify(
                    x => x.TrackDownloadCountDecrease(
                        IdA,
                        V1,
                        true,
                        true,
                        7,
                        true,
                        true,
                        5),
                    Times.Once);
            }

            [Fact]
            public void DetectsDecreaseDueToMissingVersion()
            {
                var oldData = new DownloadData();
                oldData.SetDownloadCount(IdA, V1, 7);
                oldData.SetDownloadCount(IdA, V2, 1);
                var newData = new DownloadData();
                newData.SetDownloadCount(IdA, V2, 1);

                var delta = Target.Compare(oldData, newData);

                Assert.Equal(KeyValuePair.Create(IdA, 1L), Assert.Single(delta));
                VerifyDecreaseTelemetry(Times.Once());
                TelemetryService.Verify(
                    x => x.TrackDownloadCountDecrease(
                        IdA,
                        V1,
                        true,
                        true,
                        7,
                        true,
                        false,
                        0),
                    Times.Once);
            }

            [Fact]
            public void DetectsDecreaseDueToMissingId()
            {
                var oldData = new DownloadData();
                oldData.SetDownloadCount(IdA, V1, 7);
                oldData.SetDownloadCount(IdA, V2, 1);
                oldData.SetDownloadCount(IdB, V1, 1);
                var newData = new DownloadData();
                newData.SetDownloadCount(IdB, V1, 1);

                var delta = Target.Compare(oldData, newData);

                Assert.Equal(KeyValuePair.Create(IdA, 0L), Assert.Single(delta));
                VerifyDecreaseTelemetry(Times.Exactly(2));
                TelemetryService.Verify(
                    x => x.TrackDownloadCountDecrease(
                        IdA,
                        V1,
                        true,
                        true,
                        7,
                        false,
                        false,
                        0),
                    Times.Once);
                TelemetryService.Verify(
                    x => x.TrackDownloadCountDecrease(
                        IdA,
                        V2,
                        true,
                        true,
                        1,
                        false,
                        false,
                        0),
                    Times.Once);
            }
        }

        public abstract class Facts
        {
            public const string IdA = "NuGet.Frameworks";
            public const string IdB = "NuGet.Versioning";
            public const string V1 = "1.0.0";
            public const string V2 = "2.0.0";

            public Facts(ITestOutputHelper output)
            {
                TelemetryService = new Mock<IAzureSearchTelemetryService>();
                Options = new Mock<IOptionsSnapshot<Auxiliary2AzureSearchConfiguration>>();
                Logger = output.GetLogger<DownloadSetComparer>();

                Config = new Auxiliary2AzureSearchConfiguration();
                Options.Setup(x => x.Value).Returns(() => Config);

                Target = new DownloadSetComparer(
                    TelemetryService.Object,
                    Options.Object,
                    Logger);
            }

            public Mock<IAzureSearchTelemetryService> TelemetryService { get; }
            public Mock<IOptionsSnapshot<Auxiliary2AzureSearchConfiguration>> Options { get; }
            public RecordingLogger<DownloadSetComparer> Logger { get; }
            public Auxiliary2AzureSearchConfiguration Config { get; }
            public DownloadSetComparer Target { get; }

            public void VerifyDecreaseTelemetry(Times times)
            {
                TelemetryService.Verify(
                    x => x.TrackDownloadCountDecrease(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        It.IsAny<long>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        It.IsAny<long>()),
                    times);
            }
        }
    }
}
