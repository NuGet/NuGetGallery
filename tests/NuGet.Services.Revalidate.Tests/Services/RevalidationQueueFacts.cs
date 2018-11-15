// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGetGallery;
using Tests.ContextHelpers;
using Xunit;

namespace NuGet.Services.Revalidate.Tests.Services
{
    public class RevalidationQueueFacts
    {
        public class TheNextAsyncMethod : FactsBase
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
                var nextRevalidations = await _target.NextAsync();

                // Assert
                var next = Assert.Single(nextRevalidations);
                Assert.Equal("Package", next.PackageId);
                Assert.Equal("1.0.0", next.PackageNormalizedVersion);

                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Never);
                _telemetryService.Verify(t => t.TrackFindNextRevalidations(), Times.Once);
            }

            [Fact]
            public async Task SkipsRepositorySignedPackages()
            {
                // Arrange
                _config.MaxBatchSize = 10;

                var repositorySignedPackageRevalidation = new PackageRevalidation
                {
                    Key = 1,
                    PackageId = "Repository.Signed.Package",
                    PackageNormalizedVersion = "1.0.0",
                    Enqueued = null,
                    Completed = false,
                };

                _validationContext.Mock(
                    packageRevalidations: new[]
                    {
                        repositorySignedPackageRevalidation,
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
                    },
                    new Package
                    {
                        PackageRegistration = new PackageRegistration { Id = "Repository.Signed.Package" },
                        NormalizedVersion = "1.0.0",
                        PackageStatusKey = PackageStatus.Available,
                    }
                });

                // Act
                var nextRevalidations = await _target.NextAsync();

                // Assert
                var next = Assert.Single(nextRevalidations);
                Assert.Equal("Package", next.PackageId);
                Assert.Equal("1.0.0", next.PackageNormalizedVersion);

                Assert.True(repositorySignedPackageRevalidation.Completed);

                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);
                _telemetryService.Verify(t => t.TrackFindNextRevalidations(), Times.Once);
                _telemetryService.Verify(t => t.TrackPackageRevalidationMarkedAsCompleted("Repository.Signed.Package", "1.0.0"), Times.Once);
            }

            [Theory]
            [MemberData(nameof(SkipsPackagesWithTooManyVersionsData))]
            public async Task SkipsPackagesWithTooManyVersions(int? maximumPackageVersions, bool skipsPackageWithManyVersions)
            {
                _config.MaximumPackageVersions = maximumPackageVersions;

                // Arrange
                _validationContext.Mock(packageRevalidations: new[]
                {
                    new PackageRevalidation
                    {
                        Key = 1,
                        PackageId = "Package.With.Many.Versions",
                        PackageNormalizedVersion = "1.0.0",
                        Enqueued = null,
                        Completed = false,
                    },
                    new PackageRevalidation
                    {
                        Key = 2,
                        PackageId = "Package.With.Many.Versions",
                        PackageNormalizedVersion = "2.0.0",
                        Enqueued = null,
                        Completed = false,
                    },
                    new PackageRevalidation
                    {
                        Key = 3,
                        PackageId = "Package.With.Many.Versions",
                        PackageNormalizedVersion = "3.0.0",
                        Enqueued = null,
                        Completed = false,
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
                        PackageRegistration = new PackageRegistration { Id = "Package.With.Many.Versions" },
                        NormalizedVersion = "1.0.0",
                        PackageStatusKey = PackageStatus.Available,
                    },
                    new Package
                    {
                        PackageRegistration = new PackageRegistration { Id = "Package.With.Many.Versions" },
                        NormalizedVersion = "2.0.0",
                        PackageStatusKey = PackageStatus.Available,
                    },
                    new Package
                    {
                        PackageRegistration = new PackageRegistration { Id = "Package.With.Many.Versions" },
                        NormalizedVersion = "3.0.0",
                        PackageStatusKey = PackageStatus.Available,
                    },
                    new Package
                    {
                        PackageRegistration = new PackageRegistration { Id = "Package" },
                        NormalizedVersion = "1.0.0",
                        PackageStatusKey = PackageStatus.Available,
                    },
                });

                // Act
                var nextRevalidations = await _target.NextAsync();

                // Assert
                var next = Assert.Single(nextRevalidations);
                if (skipsPackageWithManyVersions)
                {
                    Assert.Equal("Package", next.PackageId);
                    Assert.Equal("1.0.0", next.PackageNormalizedVersion);

                    _telemetryService.Verify(t => t.TrackPackageRevalidationMarkedAsCompleted("Package.With.Many.Versions", "1.0.0"), Times.Never);
                    _telemetryService.Verify(t => t.TrackPackageRevalidationMarkedAsCompleted("Package.With.Many.Versions", "2.0.0"), Times.Never);
                    _telemetryService.Verify(t => t.TrackPackageRevalidationMarkedAsCompleted("Package.With.Many.Versions", "3.0.0"), Times.Never);
                }
                else
                {
                    Assert.Equal("Package.With.Many.Versions", next.PackageId);
                    Assert.Equal("1.0.0", next.PackageNormalizedVersion);
                }

                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Never);
                _telemetryService.Verify(t => t.TrackFindNextRevalidations(), Times.Once);
            }

            public static IEnumerable<object[]> SkipsPackagesWithTooManyVersionsData()
            {
                // If the "MaximumPackageVersions" is null, no packages should be skipped for having too many versions.
                yield return new object[]
                {
                    null,
                    false
                };

                // If "MaximumPackageVersions" is set, packages with less versions than the value should not be skipped.
                yield return new object[]
                {
                    100,
                    false
                };

                // If "MaximumPackageVersions" is set, packages with more versions than the value should be skipped.
                yield return new object[]
                {
                    2,
                    true
                };
            }

            [Fact]
            public async Task SkipsDeletedPackages()
            {
                // Arrange
                _config.MaxBatchSize = 10;

                var softDeletedPackageRevalidation = new PackageRevalidation
                {
                    Key = 1,
                    PackageId = "Soft.Deleted.Package",
                    PackageNormalizedVersion = "1.0.0",
                    Enqueued = null,
                    Completed = false,
                };

                var hardDeletedPackageRevalidation = new PackageRevalidation
                {
                    Key = 2,
                    PackageId = "Hard.Deleted.Package",
                    PackageNormalizedVersion = "1.0.0",
                    Enqueued = null,
                    Completed = false,
                };

                _validationContext.Mock(packageRevalidations: new[]
                {
                    softDeletedPackageRevalidation,
                    hardDeletedPackageRevalidation,
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
                var nextRevalidations = await _target.NextAsync();

                // Assert
                var next = Assert.Single(nextRevalidations);
                Assert.Equal("Package", next.PackageId);
                Assert.Equal("1.0.0", next.PackageNormalizedVersion);

                Assert.True(softDeletedPackageRevalidation.Completed);
                Assert.True(hardDeletedPackageRevalidation.Completed);

                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);

                _telemetryService.Verify(t => t.TrackFindNextRevalidations(), Times.Once);
                _telemetryService.Verify(t => t.TrackPackageRevalidationMarkedAsCompleted("Soft.Deleted.Package", "1.0.0"), Times.Once);
                _telemetryService.Verify(t => t.TrackPackageRevalidationMarkedAsCompleted("Hard.Deleted.Package", "1.0.0"), Times.Once);
            }

            [Theory]
            [InlineData(1, false)]
            [InlineData(2, true)]
            public async Task RespectsTheMaxBatchSizeConfiguration(int maxBatchSize, bool includesSecondVersion)
            {
                // Arrange
                _config.MaxBatchSize = maxBatchSize;

                _validationContext.Mock(packageRevalidations: new[]
                {
                    new PackageRevalidation
                    {
                        Key = 1,
                        PackageId = "Package",
                        PackageNormalizedVersion = "1.0.0",
                        Enqueued = null,
                        Completed = false,
                    },
                    new PackageRevalidation
                    {
                        Key = 2,
                        PackageId = "Package",
                        PackageNormalizedVersion = "2.0.0",
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
                    },
                    new Package
                    {
                        PackageRegistration = new PackageRegistration { Id = "Package" },
                        NormalizedVersion = "2.0.0",
                        PackageStatusKey = PackageStatus.Available,
                    }
                });

                // Act
                var nextRevalidations = await _target.NextAsync();

                // Assert
                Assert.Equal(includesSecondVersion ? 2 : 1, nextRevalidations.Count);
                Assert.Equal("Package", nextRevalidations[0].PackageId);
                Assert.Equal("1.0.0", nextRevalidations[0].PackageNormalizedVersion);

                if (includesSecondVersion)
                {
                    Assert.Equal("Package", nextRevalidations[1].PackageId);
                    Assert.Equal("2.0.0", nextRevalidations[1].PackageNormalizedVersion);
                }

                _telemetryService.Verify(t => t.TrackFindNextRevalidations(), Times.Once);
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
                    MaxBatchSize = 1
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
