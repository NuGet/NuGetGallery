// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGet.Services.Validation.PackageSigning.ProcessSignature;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.Validation.PackageSigning
{
    public class PackageSignatureValidatorFacts
    {
        private const int PackageKey = 1001;
        private const string PackageId = "NuGet.Versioning";
        private const string PackageVersion = "4.3.0.0-ALPHA+git";
        private static readonly Guid ValidationId = new Guid("fb9c0bac-3d4d-4cc7-ac2d-b3940e15b94d");
        private const string NupkgUrl = "https://example/nuget.versioning/4.3.0/package.nupkg";

        public class TheGetResponseAsyncMethod : FactsBase
        {
            private static readonly ValidationStatus[] possibleValidationStatuses = new ValidationStatus[]
            {
                ValidationStatus.Incomplete,
                ValidationStatus.NotStarted,
                ValidationStatus.Succeeded,
            };

            public TheGetResponseAsyncMethod(ITestOutputHelper output) : base(output)
            {
            }

            [Theory]
            [MemberData(nameof(PossibleValidationStatuses))]
            public async Task ReturnsPersistedStatus(ValidationStatus status)
            {
                // Arrange
                _validatorStateService
                    .Setup(x => x.GetStatusAsync(It.IsAny<INuGetValidationRequest>()))
                    .ReturnsAsync(new ValidatorStatus
                    {
                        ValidationId = ValidationId,
                        PackageKey = PackageKey,
                        ValidatorName = ValidatorName.PackageSignatureProcessor,
                        State = status,
                        ValidatorIssues = new List<ValidatorIssue>(),
                    });

                // Act & Assert
                var actual = await _target.GetResponseAsync(_validationRequest.Object);

                Assert.Equal(status, actual.Status);
            }

            [Fact]
            public async Task WhenStateIsSuccess_DoesNotReturnValidatorIssues()
            {
                // Arrange
                _validatorStateService
                    .Setup(x => x.GetStatusAsync(It.IsAny<INuGetValidationRequest>()))
                    .ReturnsAsync(new ValidatorStatus
                    {
                        ValidationId = ValidationId,
                        PackageKey = PackageKey,
                        ValidatorName = ValidatorName.PackageSignatureProcessor,
                        State = ValidationStatus.Succeeded,
                        ValidatorIssues = new List<ValidatorIssue>
                        {
                            new ValidatorIssue
                            {
                                IssueCode = (ValidationIssueCode)987,
                                Data = "{}",
                            },
                            new ValidatorIssue
                            {
                                IssueCode = ValidationIssueCode.ClientSigningVerificationFailure,
                                Data = "unknown contract",
                            },
                        },
                    });

                // Act
                var actual = await _target.GetResponseAsync(_validationRequest.Object);

                // Assert
                Assert.Equal(ValidationStatus.Succeeded, actual.Status);
                Assert.Empty(actual.Issues);
            }

            [Fact]
            public async Task WhenStateIsFailedAndIssuesAreNotExpected_FailsWithException()
            {
                // Arrange
                _config.RepositorySigningEnabled = true;

                _validatorStateService
                    .Setup(x => x.GetStatusAsync(It.IsAny<INuGetValidationRequest>()))
                    .ReturnsAsync(new ValidatorStatus
                    {
                        ValidationId = ValidationId,
                        PackageKey = PackageKey,
                        ValidatorName = ValidatorName.PackageSignatureProcessor,
                        State = ValidationStatus.Failed,
                        ValidatorIssues = new List<ValidatorIssue>
                        {
                            new ValidatorIssue
                            {
                                IssueCode = ValidationIssueCode.PackageIsZip64,
                                Data = "{}",
                            },
                        },
                    });

                // Act & Assert
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _target.GetResponseAsync(_validationRequest.Object));
                Assert.Equal("Package signature validator has an unexpected validation response", ex.Message);
            }

            [Theory]
            [InlineData(ValidationIssueCode.PackageIsNotSigned)]
            [InlineData(ValidationIssueCode.PackageIsSignedWithUnauthorizedCertificate)]
            public async Task WhenStateIsFailedAndIssueCanBeCausedByOwner_ReturnsFailedValidation(ValidationIssueCode issueCode)
            {
                // Arrange
                _config.RepositorySigningEnabled = true;

                _validatorStateService
                    .Setup(x => x.GetStatusAsync(It.IsAny<INuGetValidationRequest>()))
                    .ReturnsAsync(new ValidatorStatus
                    {
                        ValidationId = ValidationId,
                        PackageKey = PackageKey,
                        ValidatorName = ValidatorName.PackageSignatureProcessor,
                        State = ValidationStatus.Failed,
                        ValidatorIssues = new List<ValidatorIssue>
                        {
                            new ValidatorIssue
                            {
                                IssueCode = issueCode,
                                Data = "{}",
                            },
                        },
                    });

                // Act
                var actual = await _target.GetResponseAsync(_validationRequest.Object);

                // Assert
                Assert.Equal(ValidationStatus.Failed, actual.Status);
                Assert.Single(actual.Issues);
                Assert.Equal(issueCode, actual.Issues.Single().IssueCode);
            }

            [Fact]
            public async Task WhenRepositorySigningEnabled_ThrowsExceptionIfValidationFails()
            {
                // Arrange
                _config.RepositorySigningEnabled = true;

                _packages
                    .Setup(p => p.FindPackageRegistrationById(_validationRequest.Object.PackageId))
                    .Returns(_packageRegistration);

                _validatorStateService
                    .Setup(x => x.GetStatusAsync(It.IsAny<INuGetValidationRequest>()))
                    .ReturnsAsync(new ValidatorStatus
                    {
                        ValidationId = ValidationId,
                        PackageKey = PackageKey,
                        ValidatorName = ValidatorName.PackageSignatureProcessor,
                        State = ValidationStatus.Failed,
                        ValidatorIssues = new List<ValidatorIssue>(),
                    });

                // Act & Assert
                var e = await Assert.ThrowsAsync<InvalidOperationException>(() => _target.GetResponseAsync(_validationRequest.Object));

                Assert.Equal("Package signature validator has an unexpected validation response", e.Message);
            }

            [Fact]
            public async Task WhenRepositorySigningEnabled_ThrowsExceptionIfPackageIsModified()
            {
                // Arrange
                _config.RepositorySigningEnabled = true;

                _packages
                    .Setup(p => p.FindPackageRegistrationById(_validationRequest.Object.PackageId))
                    .Returns(_packageRegistration);

                _validatorStateService
                    .Setup(x => x.GetStatusAsync(It.IsAny<INuGetValidationRequest>()))
                    .ReturnsAsync(new ValidatorStatus
                    {
                        ValidationId = ValidationId,
                        PackageKey = PackageKey,
                        ValidatorName = ValidatorName.PackageSignatureProcessor,
                        State = ValidationStatus.Succeeded,
                        ValidatorIssues = new List<ValidatorIssue>(),
                        NupkgUrl = "https://nuget.org/a.nupkg"
                    });

                // Act & Assert
                var e = await Assert.ThrowsAsync<InvalidOperationException>(() => _target.GetResponseAsync(_validationRequest.Object));

                Assert.Equal("Package signature validator has an unexpected validation response", e.Message);
            }

            public static IEnumerable<object[]> PossibleValidationStatuses => possibleValidationStatuses.Select(s => new object[] { s });
        }

        public class TheStartValidationAsyncMethod : FactsBase
        {
            private static readonly ValidationStatus[] startedValidationStatuses = new ValidationStatus[]
            {
                ValidationStatus.Incomplete,
                ValidationStatus.Succeeded,
            };

            public TheStartValidationAsyncMethod(ITestOutputHelper output) : base(output)
            {
            }

            [Theory]
            [MemberData(nameof(StartedValidationStatuses))]
            public async Task ReturnsPersistedStatusesIfValidationAlreadyStarted(ValidationStatus status)
            {
                // Arrange
                _validatorStateService
                     .Setup(x => x.GetStatusAsync(It.IsAny<INuGetValidationRequest>()))
                     .ReturnsAsync(new ValidatorStatus
                     {
                         ValidationId = ValidationId,
                         PackageKey = PackageKey,
                         ValidatorName = ValidatorName.PackageSignatureProcessor,
                         State = status,
                         ValidatorIssues = new List<ValidatorIssue>(),
                     });

                // Act & Assert
                await _target.StartAsync(_validationRequest.Object);

                _packageSignatureVerifier
                    .Verify(x => x.EnqueueProcessSignatureAsync(It.IsAny<INuGetValidationRequest>(), It.IsAny<bool>()), Times.Never);

                _validatorStateService
                    .Verify(x => x.AddStatusAsync(It.IsAny<ValidatorStatus>()), Times.Never);

                _telemetryService.Verify(
                    x => x.TrackDurationToStartPackageSigningValidator(It.IsAny<string>(), It.IsAny<string>()),
                    Times.Never);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task StartsValidationIfNotStarted(bool repositorySigningEnabled)
            {
                // Arrange
                // The order of operations is important! The state MUST be persisted AFTER verification has been queued.
                var statePersisted = false;
                bool verificationQueuedBeforeStatePersisted = false;

                _config.RepositorySigningEnabled = repositorySigningEnabled;

                _validatorStateService
                     .Setup(x => x.GetStatusAsync(It.IsAny<INuGetValidationRequest>()))
                     .ReturnsAsync(new ValidatorStatus
                     {
                         ValidationId = ValidationId,
                         PackageKey = PackageKey,
                         ValidatorName = ValidatorName.PackageSignatureProcessor,
                         State = ValidationStatus.NotStarted,
                         ValidatorIssues = new List<ValidatorIssue>(),
                     });

                _packageSignatureVerifier
                    .Setup(x => x.EnqueueProcessSignatureAsync(It.IsAny<INuGetValidationRequest>(), true))
                    .Callback(() =>
                    {
                        verificationQueuedBeforeStatePersisted = !statePersisted;
                    })
                    .Returns(Task.FromResult(0));

                _validatorStateService
                    .Setup(x => x.TryAddValidatorStatusAsync(
                                    It.IsAny<INuGetValidationRequest>(),
                                    It.IsAny<ValidatorStatus>(),
                                    It.IsAny<ValidationStatus>()))
                    .Callback(() =>
                    {
                        statePersisted = true;
                    })
                    .ReturnsAsync(new ValidatorStatus
                    {
                        State = ValidationStatus.Incomplete,
                        ValidatorIssues = new List<ValidatorIssue>(),
                    });

                // Act
                await _target.StartAsync(_validationRequest.Object);

                // Assert
                _packageSignatureVerifier
                    .Verify(x => x.EnqueueProcessSignatureAsync(It.IsAny<INuGetValidationRequest>(), true), Times.Once);

                _validatorStateService
                    .Verify(
                        x => x.TryAddValidatorStatusAsync(
                                It.IsAny<INuGetValidationRequest>(),
                                It.IsAny<ValidatorStatus>(),
                                It.Is<ValidationStatus>(s => s == ValidationStatus.Incomplete)),
                        Times.Once);

                _telemetryService.Verify(
                    x => x.TrackDurationToStartPackageSigningValidator(_validationRequest.Object.PackageId, _validationRequest.Object.PackageVersion),
                    Times.Once);

                Assert.True(verificationQueuedBeforeStatePersisted);
            }

            [Fact]
            public async Task DoesNotReturnValidatorIssues()
            {
                // Arrange
                _validatorStateService
                    .Setup(x => x.GetStatusAsync(It.IsAny<INuGetValidationRequest>()))
                    .ReturnsAsync(new ValidatorStatus
                    {
                        ValidationId = ValidationId,
                        PackageKey = PackageKey,
                        ValidatorName = ValidatorName.PackageSignatureProcessor,
                        State = ValidationStatus.Succeeded,
                        ValidatorIssues = new List<ValidatorIssue>
                        {
                            new ValidatorIssue
                            {
                                IssueCode = (ValidationIssueCode)987,
                                Data = "{}",
                            },
                            new ValidatorIssue
                            {
                                IssueCode = ValidationIssueCode.ClientSigningVerificationFailure,
                                Data = "unknown contract",
                            },
                        },
                    });

                // Act
                var actual = await _target.StartAsync(_validationRequest.Object);

                // Assert
                Assert.Equal(ValidationStatus.Succeeded, actual.Status);
                Assert.Empty(actual.Issues);
            }

            [Fact]
            public async Task WhenRepositorySigningEnabled_ThrowsExceptionIfValidationFails()
            {
                // Arrange
                _config.RepositorySigningEnabled = true;

                _validatorStateService
                    .Setup(x => x.GetStatusAsync(It.IsAny<INuGetValidationRequest>()))
                    .ReturnsAsync(new ValidatorStatus
                    {
                        ValidationId = ValidationId,
                        PackageKey = PackageKey,
                        ValidatorName = ValidatorName.PackageSignatureProcessor,
                        State = ValidationStatus.Failed,
                        ValidatorIssues = new List<ValidatorIssue>(),
                    });

                // Act
                await Assert.ThrowsAsync<InvalidOperationException>(() => _target.StartAsync(_validationRequest.Object));
            }

            [Fact]
            public async Task WhenRepositorySigningEnabled_ThrowsExceptionIfPackageIsModified()
            {
                // Arrange
                _config.RepositorySigningEnabled = true;

                _validatorStateService
                    .Setup(x => x.GetStatusAsync(It.IsAny<INuGetValidationRequest>()))
                    .ReturnsAsync(new ValidatorStatus
                    {
                        ValidationId = ValidationId,
                        PackageKey = PackageKey,
                        ValidatorName = ValidatorName.PackageSignatureProcessor,
                        State = ValidationStatus.Succeeded,
                        ValidatorIssues = new List<ValidatorIssue>(),
                        NupkgUrl = "https://nuget.org/a.nupkg"
                    });

                // Act
                await Assert.ThrowsAsync<InvalidOperationException>(() => _target.StartAsync(_validationRequest.Object));
            }

            [Fact]
            public async Task WhenRepositorySigningDisabled_DoesNotThrowExceptionIfValidationFails()
            {
                // Arrange
                _config.RepositorySigningEnabled = false;

                _validatorStateService
                    .Setup(x => x.GetStatusAsync(It.IsAny<INuGetValidationRequest>()))
                    .ReturnsAsync(new ValidatorStatus
                    {
                        ValidationId = ValidationId,
                        PackageKey = PackageKey,
                        ValidatorName = ValidatorName.PackageSignatureProcessor,
                        State = ValidationStatus.Failed,
                        ValidatorIssues = new List<ValidatorIssue>(),
                    });

                // Act
                var actual = await _target.StartAsync(_validationRequest.Object);

                // Assert
                Assert.Equal(ValidationStatus.Succeeded, actual.Status);
                Assert.Empty(actual.Issues);
                Assert.Null(actual.NupkgUrl);
            }

            [Fact]
            public async Task WhenRepositorySigningDisabled_DoesNotThrowExceptionIfPackageIsModified()
            {
                // Arrange
                _config.RepositorySigningEnabled = false;

                _validatorStateService
                    .Setup(x => x.GetStatusAsync(It.IsAny<INuGetValidationRequest>()))
                    .ReturnsAsync(new ValidatorStatus
                    {
                        ValidationId = ValidationId,
                        PackageKey = PackageKey,
                        ValidatorName = ValidatorName.PackageSignatureProcessor,
                        State = ValidationStatus.Succeeded,
                        ValidatorIssues = new List<ValidatorIssue>(),
                        NupkgUrl = "https://nuget.org/a.nupkg"
                    });

                // Act
                var actual = await _target.StartAsync(_validationRequest.Object);

                // Assert
                Assert.Equal(ValidationStatus.Succeeded, actual.Status);
                Assert.Empty(actual.Issues);
                Assert.Null(actual.NupkgUrl);
            }

            public static IEnumerable<object[]> StartedValidationStatuses => startedValidationStatuses.Select(s => new object[] { s });
        }

        public class TheCleanUpAsyncMethod : FactsBase
        {
            private readonly ValidatorStatus _validatorStatus;
            private readonly Mock<ISimpleCloudBlob> _blob;

            public TheCleanUpAsyncMethod(ITestOutputHelper output) : base(output)
            {
                _validatorStatus = new ValidatorStatus();
                _validatorStateService
                    .Setup(x => x.GetStatusAsync(It.IsAny<INuGetValidationRequest>()))
                    .ReturnsAsync(() => _validatorStatus);

                _blob = new Mock<ISimpleCloudBlob>(MockBehavior.Strict);
                _blobProvider
                    .Setup(x => x.GetBlobFromUrl(It.IsAny<string>()))
                    .Returns(() => _blob.Object);
            }

            [Fact]
            public async Task DeletesNothingWhenThereIsNoNupkgUrl()
            {
                await _target.CleanUpAsync(_validationRequest.Object);

                _validatorStateService.Verify(x => x.GetStatusAsync(_validationRequest.Object), Times.Once);
                _blobProvider.Verify(x => x.GetBlobFromUrl(It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task DeletesTheBlobWhenThereIsNupkgUrl()
            {
                var nupkgUrl = "http://example/packages/nuget.versioning.4.6.0.nupkg";
                _validatorStatus.NupkgUrl = nupkgUrl;
                _blob
                    .Setup(x => x.DeleteIfExistsAsync())
                    .Returns(Task.CompletedTask);

                await _target.CleanUpAsync(_validationRequest.Object);

                _validatorStateService.Verify(x => x.GetStatusAsync(_validationRequest.Object), Times.Once);
                _blobProvider.Verify(x => x.GetBlobFromUrl(nupkgUrl), Times.Once);
                _blob.Verify(x => x.DeleteIfExistsAsync(), Times.Once);
            }
        }

        public abstract class FactsBase
        {
            protected readonly Mock<IValidatorStateService> _validatorStateService;
            protected readonly Mock<IProcessSignatureEnqueuer> _packageSignatureVerifier;
            protected readonly Mock<ISimpleCloudBlobProvider> _blobProvider;
            protected readonly Mock<ICorePackageService> _packages;
            protected readonly Mock<IOptionsSnapshot<ScanAndSignConfiguration>> _configAccessor;
            protected readonly Mock<ITelemetryService> _telemetryService;
            protected readonly ILogger<PackageSignatureValidator> _logger;
            protected readonly Mock<INuGetValidationRequest> _validationRequest;
            protected readonly PackageSignatureValidator _target;

            protected readonly ScanAndSignConfiguration _config;
            protected readonly PackageRegistration _packageRegistration;

            public FactsBase(ITestOutputHelper output)
            {
                _validatorStateService = new Mock<IValidatorStateService>();
                _packageSignatureVerifier = new Mock<IProcessSignatureEnqueuer>();
                _blobProvider = new Mock<ISimpleCloudBlobProvider>();
                _packages = new Mock<ICorePackageService>();
                _config = new ScanAndSignConfiguration();
                _configAccessor = new Mock<IOptionsSnapshot<ScanAndSignConfiguration>>();
                _telemetryService = new Mock<ITelemetryService>();
                var loggerFactory = new LoggerFactory().AddXunit(output);
                _logger = loggerFactory.CreateLogger<PackageSignatureValidator>();

                _validationRequest = new Mock<INuGetValidationRequest>();
                _validationRequest.Setup(x => x.NupkgUrl).Returns(NupkgUrl);
                _validationRequest.Setup(x => x.PackageId).Returns(PackageId);
                _validationRequest.Setup(x => x.PackageKey).Returns(PackageKey);
                _validationRequest.Setup(x => x.PackageVersion).Returns(PackageVersion);
                _validationRequest.Setup(x => x.ValidationId).Returns(ValidationId);

                _configAccessor.Setup(a => a.Value).Returns(_config);

                _packageRegistration = new PackageRegistration
                {
                    Owners = new[]
                    {
                        new User { Username = "GoodUsername" }
                    }
                };

                _target = new PackageSignatureValidator(
                    _validatorStateService.Object,
                    _packageSignatureVerifier.Object,
                    _blobProvider.Object,
                    _packages.Object,
                    _configAccessor.Object,
                    _telemetryService.Object,
                    _logger);
            }
        }
    }
}
