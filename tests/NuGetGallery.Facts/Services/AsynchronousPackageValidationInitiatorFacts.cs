﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Validation;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using Xunit;

namespace NuGetGallery
{
    public class AsynchronousPackageValidationInitiatorFacts
    {
        public class TheStartValidationAsyncMethod : FactsBase
        {
            [Fact]
            public async Task UsesADifferentValidationTrackingIdEachTime()
            {
                // Arrange
                var package = GetPackage();

                // Act
                await _target.StartValidationAsync(package);
                await _target.StartValidationAsync(package);

                // Assert
                Assert.Equal(2, _data.Count);
                Assert.NotEqual(_data[0].ValidationTrackingId, _data[1].ValidationTrackingId);
            }

            [Fact]
            public async Task UsesProvidedPackageIdAndVersion()
            {
                // Arrange
                var package = GetPackage();

                // Act
                await _target.StartValidationAsync(package);

                // Assert
                _enqueuer.Verify(
                    x => x.StartValidationAsync(It.IsAny<PackageValidationMessageData>(), It.IsAny<DateTimeOffset>()),
                    Times.Once);
                Assert.Equal(1, _data.Count);
                Assert.NotNull(_data[0]);
                Assert.Equal(package.PackageRegistration.Id, _data[0].PackageId);
                Assert.Equal(package.Version, _data[0].PackageVersion);
            }

            [Fact]
            public async Task FailsWhenTheGalleryIsInReadOnlyMode()
            {
                // Arrange
                var package = GetPackage();
                _appConfiguration
                    .Setup(x => x.ReadOnlyMode)
                    .Returns(true);

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ReadOnlyModeException>(
                    () => _target.StartValidationAsync(package));
                Assert.Equal(Strings.CannotEnqueueDueToReadOnly, exception.Message);
                _enqueuer.Verify(
                    x => x.StartValidationAsync(It.IsAny<PackageValidationMessageData>()),
                    Times.Never);
            }

            [Fact]
            public async Task ReturnsCorrectPackageStatusInNonBlockingMode()
            {
                // Arrange
                var package = GetPackage();
                _appConfiguration
                    .SetupGet(x => x.BlockingAsynchronousPackageValidationEnabled)
                    .Returns(false);

                // Act
                var actual = await _target.StartValidationAsync(package);

                // Assert
                Assert.Equal(PackageStatus.Available, actual);
            }

            [Fact]
            public async Task ReturnsCorrectPackageStatusInBlockingMode()
            {
                // Arrange
                var package = GetPackage();
                _appConfiguration
                    .SetupGet(x => x.BlockingAsynchronousPackageValidationEnabled)
                    .Returns(true);

                // Act
                var actual = await _target.StartValidationAsync(package);

                // Assert
                Assert.Equal(PackageStatus.Validating, actual);
            }

            private Package GetPackage()
            {
                return new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "NuGet.Versioning"
                    },
                    Version = "4.3.0",
                    PackageStatusKey = (PackageStatus)(-1),
                };
            }
        }

        public class TheStartSymbolsPackageValidationAsyncMethod : FactsBase
        {
            [Fact]
            public async Task UsesADifferentValidationTrackingIdEachTime()
            {
                // Arrange
                var symbolPackage = GetSymbolPackage();

                // Act
                await _target.StartSymbolsPackageValidationAsync(symbolPackage);
                await _target.StartSymbolsPackageValidationAsync(symbolPackage);

                // Assert
                Assert.Equal(2, _data.Count);
                Assert.NotEqual(_data[0].ValidationTrackingId, _data[1].ValidationTrackingId);
            }

            [Fact]
            public async Task UsesProvidedPackageIdAndVersion()
            {
                // Arrange
                var symbolPackage = GetSymbolPackage();

                // Act
                await _target.StartSymbolsPackageValidationAsync(symbolPackage);

                // Assert
                _symbolEnqueuer.Verify(
                    x => x.StartValidationAsync(It.IsAny<PackageValidationMessageData>(), It.IsAny<DateTimeOffset>()),
                    Times.Once);
                Assert.Equal(1, _data.Count);
                Assert.NotNull(_data[0]);
                Assert.Equal(symbolPackage.Package.PackageRegistration.Id, _data[0].PackageId);
                Assert.Equal(symbolPackage.Package.Version, _data[0].PackageVersion);
            }

            [Fact]
            public async Task FailsWhenTheGalleryIsInReadOnlyMode()
            {
                // Arrange
                var symbolPackage = GetSymbolPackage();
                _appConfiguration
                    .Setup(x => x.ReadOnlyMode)
                    .Returns(true);

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ReadOnlyModeException>(
                    () => _target.StartSymbolsPackageValidationAsync(symbolPackage));
                Assert.Equal(Strings.CannotEnqueueDueToReadOnly, exception.Message);
                _symbolEnqueuer.Verify(
                    x => x.StartValidationAsync(It.IsAny<PackageValidationMessageData>()),
                    Times.Never);
            }

            [Fact]
            public async Task ReturnsCorrectPackageStatusInNonBlockingMode()
            {
                // Arrange
                var symbolPackage = GetSymbolPackage();
                _appConfiguration
                    .SetupGet(x => x.BlockingAsynchronousPackageValidationEnabled)
                    .Returns(false);

                // Act
                var actual = await _target.StartSymbolsPackageValidationAsync(symbolPackage);

                // Assert
                Assert.Equal(PackageStatus.Available, actual);
            }

            [Fact]
            public async Task ReturnsCorrectPackageStatusInBlockingMode()
            {
                // Arrange
                var symbolPackage = GetSymbolPackage();
                _appConfiguration
                    .SetupGet(x => x.BlockingAsynchronousPackageValidationEnabled)
                    .Returns(true);

                // Act
                var actual = await _target.StartSymbolsPackageValidationAsync(symbolPackage);

                // Assert
                Assert.Equal(PackageStatus.Validating, actual);
            }

            private SymbolPackage GetSymbolPackage()
            {
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "NuGet.Versioning"
                    },
                    Version = "4.3.0",
                    PackageStatusKey = (PackageStatus)(-1),
                };

                return new SymbolPackage()
                {
                    Package = package
                };
            }
        }

        public abstract class FactsBase
        {
            protected readonly Mock<IPackageValidationEnqueuer> _enqueuer;
            protected readonly Mock<IPackageValidationEnqueuer> _symbolEnqueuer;
            protected readonly Mock<IAppConfiguration> _appConfiguration;
            protected readonly Mock<IDiagnosticsService> _diagnosticsService;
            protected readonly IList<PackageValidationMessageData> _data = new List<PackageValidationMessageData>();
            protected readonly AsynchronousPackageValidationInitiator _target;

            public FactsBase()
            {
                _enqueuer = new Mock<IPackageValidationEnqueuer>();
                _enqueuer
                    .Setup(x => x.StartValidationAsync(It.IsAny<PackageValidationMessageData>(), It.IsAny<DateTimeOffset>()))
                    .Returns(Task.CompletedTask)
                    .Callback<PackageValidationMessageData, DateTimeOffset>((d, o) => _data.Add(d));

                _symbolEnqueuer = new Mock<IPackageValidationEnqueuer>();
                _symbolEnqueuer
                    .Setup(x => x.StartValidationAsync(It.IsAny<PackageValidationMessageData>(), It.IsAny<DateTimeOffset>()))
                    .Returns(Task.CompletedTask)
                    .Callback<PackageValidationMessageData, DateTimeOffset>((d, o) => _data.Add(d));

                _appConfiguration = new Mock<IAppConfiguration>();
                _appConfiguration
                    .Setup(x => x.ReadOnlyMode)
                    .Returns(false);

                _diagnosticsService = new Mock<IDiagnosticsService>();

                _target = new AsynchronousPackageValidationInitiator(
                    _enqueuer.Object,
                    _symbolEnqueuer.Object,
                    _appConfiguration.Object,
                    _diagnosticsService.Object);
            }
        }
    }
}
