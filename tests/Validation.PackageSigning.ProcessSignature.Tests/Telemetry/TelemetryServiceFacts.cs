// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Jobs.Validation.PackageSigning.Telemetry;
using NuGet.Packaging.Signing;
using NuGet.Services.Logging;
using Xunit;

namespace Validation.PackageSigning.ProcessSignature.Tests.Telemetry
{
    public class TelemetryServiceFacts
    {
        public class TrackStrippedRepositorySignatures : Facts
        {
            [Fact]
            public async Task EmitsExpectedMetric()
            {
                // Arrange
                var inputSignature = await GetAuthorAndRepoSignatureAsync();
                var outputSignature = await GetAuthorSignatureAsync();

                // Act
                _target.TrackStrippedRepositorySignatures(
                    _packageId,
                    _normalizedVersion,
                    _validationId,
                    inputSignature,
                    outputSignature);

                // Assert
                _telemetryClient.Verify(
                    x => x.TrackMetric("ProcessSignature.StrippedRepositorySignatures", 1, It.IsAny<IDictionary<string, string>>()),
                    Times.Once);
                Assert.NotNull(_properties);
                Assert.Equal(
                    new[] { "InputCounterSignatureCount", "InputSignatureType", "NormalizedVersion", "OutputCounterSignatureCount", "OutputSignatureType", "PackageId", "ValidationId" },
                    _properties.Keys.OrderBy(x => x).ToArray());
                Assert.Equal("Author", _properties["InputSignatureType"]);
                Assert.Equal("1", _properties["InputCounterSignatureCount"]);
                Assert.Equal(_normalizedVersion, _properties["NormalizedVersion"]);
                Assert.Equal("Author", _properties["OutputSignatureType"]);
                Assert.Equal("0", _properties["OutputCounterSignatureCount"]);
                Assert.Equal(_packageId, _properties["PackageId"]);
                Assert.Equal(_validationId.ToString(), _properties["ValidationId"]);
            }

            [Fact]
            public async Task HandlesNullOutputSignature()
            {
                // Arrange
                var inputSignature = await GetRepositorySignatureAsync();
                PrimarySignature outputSignature = null;

                // Act
                _target.TrackStrippedRepositorySignatures(
                    _packageId,
                    _normalizedVersion,
                    _validationId,
                    inputSignature,
                    outputSignature);

                // Assert
                _telemetryClient.Verify(
                    x => x.TrackMetric("ProcessSignature.StrippedRepositorySignatures", 1, It.IsAny<IDictionary<string, string>>()),
                    Times.Once);
                Assert.NotNull(_properties);
                Assert.Equal(
                    new[] { "InputCounterSignatureCount", "InputSignatureType",  "NormalizedVersion", "PackageId", "ValidationId" },
                    _properties.Keys.OrderBy(x => x).ToArray());
                Assert.Equal("Repository", _properties["InputSignatureType"]);
                Assert.Equal("0", _properties["InputCounterSignatureCount"]);
                Assert.Equal(_normalizedVersion, _properties["NormalizedVersion"]);
                Assert.Equal(_packageId, _properties["PackageId"]);
                Assert.Equal(_validationId.ToString(), _properties["ValidationId"]);
            }
        }

        public class TrackDurationToStripRepositorySignatures : Facts
        {
            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void EmitsExpectedMetric(bool changed)
            {
                // Arrange & Act
                _target.TrackDurationToStripRepositorySignatures(
                    _duration,
                    _packageId,
                    _normalizedVersion,
                    _validationId,
                    changed);

                // Assert
                _telemetryClient.Verify(
                    x => x.TrackMetric("ProcessSignature.DurationToStripRepositorySignaturesSeconds", _duration.TotalSeconds, It.IsAny<IDictionary<string, string>>()),
                    Times.Once);
                Assert.NotNull(_properties);
                Assert.Equal(
                    new[] { "Changed", "NormalizedVersion", "PackageId", "ValidationId" },
                    _properties.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(changed.ToString(), _properties["Changed"]);
                Assert.Equal(_normalizedVersion, _properties["NormalizedVersion"]);
                Assert.Equal(_packageId, _properties["PackageId"]);
                Assert.Equal(_validationId.ToString(), _properties["ValidationId"]);
            }
        }

        public abstract class Facts
        {
            protected IDictionary<string, string> _properties;
            protected readonly string _packageId;
            protected readonly string _normalizedVersion;
            protected readonly Guid _validationId;
            protected readonly TimeSpan _duration;
            protected readonly Mock<ITelemetryClient> _telemetryClient;
            protected readonly TelemetryService _target;

            public Facts()
            {
                _packageId = "NuGet.Versioning";
                _normalizedVersion = "4.6.0-BETA";
                _validationId = new Guid("f6fa40eb-6bd0-4470-a7db-da7c331de26c");
                _duration = TimeSpan.FromTicks(42023);
                
                _telemetryClient = new Mock<ITelemetryClient>();
                _telemetryClient
                    .Setup(x => x.TrackMetric(It.IsAny<string>(), It.IsAny<double>(), It.IsAny<IDictionary<string, string>>()))
                    .Callback<string, double, IDictionary<string, string>>((_, __, p) => _properties = p);

                _target = new TelemetryService(_telemetryClient.Object);
            }

            public Task<PrimarySignature> GetAuthorAndRepoSignatureAsync()
            {
                return TestResources.LoadPrimarySignatureAsync(TestResources.AuthorAndRepoSignedPackageLeaf1);
            }

            public Task<PrimarySignature> GetAuthorSignatureAsync()
            {
                return TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1);
            }

            public Task<PrimarySignature> GetRepositorySignatureAsync()
            {
                return TestResources.LoadPrimarySignatureAsync(TestResources.RepoSignedPackageLeaf1);
            }
        }
    }
}
