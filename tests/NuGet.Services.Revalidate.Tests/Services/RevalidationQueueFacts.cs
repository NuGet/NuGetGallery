// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Validation;
using NuGetGallery;
using Tests.ContextHelpers;
using Xunit;

namespace NuGet.Services.Revalidate.Tests.Services
{
    public class RevalidationQueueFacts
    {
        public class TheNextOrNullAsyncMethod : FactsBase
        {
            [Fact]
            public async Task SkipsEnqueuedOrCompletedRevalidations()
            {
                // Arrange
                _validationContext.Mock(packageRevalidations: new[]
                {
                    new PackageRevalidation
                    {
                        Key = 1,
                        PackageId = "Enqueued.Package",
                        PackageNormalizedVersion = "1.0.0",
                        Enqueued = DateTime.UtcNow,
                        Completed = false,
                    },
                    new PackageRevalidation
                    {
                        Key = 2,
                        PackageId = "Completed.Package",
                        PackageNormalizedVersion = "1.0.0",
                        Enqueued = null,
                        Completed = true,
                    },
                    new PackageRevalidation
                    {
                        Key = 3,
                        PackageId = "Enqueued.And.Completed.Package",
                        PackageNormalizedVersion = "1.0.0",
                        Enqueued = DateTime.UtcNow,
                        Completed = true,
                    },
                    new PackageRevalidation
                    {
                        Key = 4,
                        PackageId = "Package",
                        PackageNormalizedVersion = "1.0.0",
                        Enqueued = null,
                        Completed = false,
                    },
                });

                _galleryContext.Mock(packages: new[]
                {
                    new Package
                    {
                        PackageRegistration = new PackageRegistration { Id = "Package" },
                        NormalizedVersion = "1.0.0",
                        PackageStatusKey = PackageStatus.Available,
                    }
                });

                // Act
                var next = await _target.NextOrNullAsync();

                // Assert
                Assert.Equal("Package", next.PackageId);
                Assert.Equal("1.0.0", next.PackageNormalizedVersion);
            }

            [Fact]
            public async Task SkipsRepositorySignedPackages()
            {
                // Arrange
                _validationContext.Mock(
                    packageRevalidations: new[]
                    {
                        new PackageRevalidation
                        {
                            Key = 1,
                            PackageId = "Repository.Signed.Package",
                            PackageNormalizedVersion = "1.0.0",
                            Enqueued = null,
                            Completed = false,
                        },
                        new PackageRevalidation
                        {
                            Key = 2,
                            PackageId = "Package",
                            PackageNormalizedVersion = "1.0.0",
                            Enqueued = null,
                            Completed = false,
                        },
                    },
                    packageSigningStates: new[]
                    {
                        new PackageSigningState
                        {
                            PackageId = "Repository.Signed.Package",
                            PackageNormalizedVersion = "1.0.0",

                            PackageSignatures = new[]
                            {
                                new PackageSignature { Type = PackageSignatureType.Repository }
                            }
                        }
                    });

                _galleryContext.Mock(packages: new[]
                {
                    new Package
                    {
                        PackageRegistration = new PackageRegistration { Id = "Package" },
                        NormalizedVersion = "1.0.0",
                        PackageStatusKey = PackageStatus.Available,
                    }
                });

                // Act
                var next = await _target.NextOrNullAsync();

                // Assert
                Assert.Equal("Package", next.PackageId);
                Assert.Equal("1.0.0", next.PackageNormalizedVersion);

                _telemetryService.Verify(t => t.TrackPackageRevalidationMarkedAsCompleted("Repository.Signed.Package", "1.0.0"), Times.Once);
            }

            [Fact]
            public async Task SkipsDeletedPackages()
            {
                // Arrange
                _validationContext.Mock(packageRevalidations: new[]
                {
                    new PackageRevalidation
                    {
                        Key = 1,
                        PackageId = "Soft.Deleted.Package",
                        PackageNormalizedVersion = "1.0.0",
                        Enqueued = null,
                        Completed = false,
                    },
                    new PackageRevalidation
                    {
                        Key = 2,
                        PackageId = "Hard.Deleted.Package",
                        PackageNormalizedVersion = "1.0.0",
                        Enqueued = null,
                        Completed = false,
                    },
                    new PackageRevalidation
                    {
                        Key = 3,
                        PackageId = "Package",
                        PackageNormalizedVersion = "1.0.0",
                        Enqueued = null,
                        Completed = false,
                    },
                });

                _galleryContext.Mock(packages: new[]
                {
                    new Package
                    {
                        PackageRegistration = new PackageRegistration { Id = "Soft.Deleted.Package" },
                        NormalizedVersion = "1.0.0",
                        PackageStatusKey = PackageStatus.Deleted,
                    },
                    new Package
                    {
                        PackageRegistration = new PackageRegistration { Id = "Package" },
                        NormalizedVersion = "1.0.0",
                        PackageStatusKey = PackageStatus.Available,
                    }
                });

                // Act
                var next = await _target.NextOrNullAsync();

                // Assert
                Assert.Equal("Package", next.PackageId);
                Assert.Equal("1.0.0", next.PackageNormalizedVersion);

                _telemetryService.Verify(t => t.TrackPackageRevalidationMarkedAsCompleted("Soft.Deleted.Package", "1.0.0"), Times.Once);
                _telemetryService.Verify(t => t.TrackPackageRevalidationMarkedAsCompleted("Hard.Deleted.Package", "1.0.0"), Times.Once);
            }

            [Fact]
            public async Task IfReachesAttemptsThreshold_ReturnsNull()
            {
                // Arrange
                _config.MaximumAttempts = 1;

                _validationContext.Mock(packageRevalidations: new[]
                {
                    new PackageRevalidation
                    {
                        Key = 1,
                        PackageId = "Hard.Deleted.Package",
                        PackageNormalizedVersion = "1.0.0",
                        Enqueued = null,
                        Completed = false,
                    },
                    new PackageRevalidation
                    {
                        Key = 2,
                        PackageId = "Package",
                        PackageNormalizedVersion = "1.0.0",
                        Enqueued = null,
                        Completed = false,
                    },
                });

                _galleryContext.Mock(packages: new[]
                {
                    new Package
                    {
                        PackageRegistration = new PackageRegistration { Id = "Package" },
                        NormalizedVersion = "1.0.0",
                        PackageStatusKey = PackageStatus.Available,
                    }
                });

                // Act
                var next = await _target.NextOrNullAsync();

                // Assert
                Assert.Null(next);

                _telemetryService.Verify(t => t.TrackPackageRevalidationMarkedAsCompleted("Hard.Deleted.Package", "1.0.0"), Times.Once);
            }
        }

        public class FactsBase
        {
            protected readonly Mock<IEntitiesContext> _galleryContext;
            protected readonly Mock<IValidationEntitiesContext> _validationContext;
            protected readonly RevalidationQueueConfiguration _config;
            protected readonly Mock<ITelemetryService> _telemetryService;

            protected readonly RevalidationQueue _target;

            public FactsBase()
            {
                _galleryContext = new Mock<IEntitiesContext>();
                _validationContext = new Mock<IValidationEntitiesContext>();
                _telemetryService = new Mock<ITelemetryService>();

                _config = new RevalidationQueueConfiguration
                {
                    MaximumAttempts = 5,
                    SleepBetweenAttempts = TimeSpan.FromSeconds(0)
                };

                _target = new RevalidationQueue(
                    _galleryContext.Object,
                    _validationContext.Object,
                    _config,
                    _telemetryService.Object,
                    Mock.Of<ILogger<RevalidationQueue>>());
            }
        }
    }
}
