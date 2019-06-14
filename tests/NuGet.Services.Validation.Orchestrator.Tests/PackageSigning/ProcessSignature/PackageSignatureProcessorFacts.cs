// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGet.Services.Validation.PackageSigning.ProcessSignature;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.Validation.PackageSigning
{
    public class PackageSignatureProcessorFacts
    {
        private const int PackageKey = 1001;
        private const string PackageId = "NuGet.Versioning";
        private const string PackageVersion = "4.3.0.0-ALPHA+git";
        private static readonly Guid ValidationId = new Guid("fb9c0bac-3d4d-4cc7-ac2d-b3940e15b94d");
        private const string NupkgUrl = "https://example/nuget.versioning/4.3.0/package.nupkg";

        public class TheGetStatusMethod : FactsBase
        {
            private static readonly ValidationStatus[] possibleValidationStatuses = new ValidationStatus[]
            {
                ValidationStatus.Failed,
                ValidationStatus.Incomplete,
                ValidationStatus.NotStarted,
                ValidationStatus.Succeeded,
            };

            public TheGetStatusMethod(ITestOutputHelper output) : base(output)
            {
            }

            [Theory]
            [MemberData(nameof(PossibleValidationStatuses))]
            public async Task ReturnsPersistedStatus(ValidationStatus status)
            {
                // Arrange
                _validatorStateService
                    .Setup(x => x.GetStatusAsync(It.IsAny<IValidationRequest>()))
                    .ReturnsAsync(new ValidatorStatus
                    {
                        ValidationId = ValidationId,
                        PackageKey = PackageKey,
                        ValidatorName = ValidatorName.PackageSignatureProcessor,
                        State = status,
                        ValidatorIssues = new List<ValidatorIssue>(),
                    });

                // Act & Assert
                var actual = await _target.GetResultAsync(_validationRequest.Object);

                Assert.Equal(status, actual.Status);
            }

            [Fact]
            public async Task ReturnsValidatorIssues()
            {
                // Arrange
                _validatorStateService
                    .Setup(x => x.GetStatusAsync(It.IsAny<IValidationRequest>()))
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
                var actual = await _target.GetResultAsync(_validationRequest.Object);

                // Assert
                Assert.Equal(ValidationStatus.Failed, actual.Status);
                Assert.Equal(2, actual.Issues.Count);

                Assert.Equal((ValidationIssueCode)987, actual.Issues[0].IssueCode);
                Assert.Equal("{}", actual.Issues[0].Serialize());

                Assert.Equal(ValidationIssueCode.ClientSigningVerificationFailure, actual.Issues[1].IssueCode);
                Assert.Equal("unknown contract", actual.Issues[1].Serialize());
            }

            public static IEnumerable<object[]> PossibleValidationStatuses => possibleValidationStatuses.Select(s => new object[] { s });
        }

        public class TheStartValidationAsyncMethod : FactsBase
        {
            private static readonly ValidationStatus[] startedValidationStatuses = new ValidationStatus[]
            {
                ValidationStatus.Failed,
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
                     .Setup(x => x.GetStatusAsync(It.IsAny<IValidationRequest>()))
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
                    .Verify(x => x.EnqueueProcessSignatureAsync(It.IsAny<IValidationRequest>(), It.IsAny<bool>()), Times.Never);

                _validatorStateService
                    .Verify(x => x.AddStatusAsync(It.IsAny<ValidatorStatus>()), Times.Never);

                _telemetryService.Verify(
                    x => x.TrackDurationToStartPackageSigningValidator(It.IsAny<string>(), It.IsAny<string>()),
                    Times.Never);
            }

            [Fact]
            public async Task StartsValidationIfNotStarted()
            {
                // Arrange
                // The order of operations is important! The state MUST be persisted AFTER verification has been queued.
                var statePersisted = false;
                bool verificationQueuedBeforeStatePersisted = false;

                _validatorStateService
                     .Setup(x => x.GetStatusAsync(It.IsAny<IValidationRequest>()))
                     .ReturnsAsync(new ValidatorStatus
                     {
                         ValidationId = ValidationId,
                         PackageKey = PackageKey,
                         ValidatorName = ValidatorName.PackageSignatureProcessor,
                         State = ValidationStatus.NotStarted,
                         ValidatorIssues = new List<ValidatorIssue>(),
                     });

                _packageSignatureVerifier
                    .Setup(x => x.EnqueueProcessSignatureAsync(It.IsAny<IValidationRequest>(), false))
                    .Callback(() =>
                    {
                        verificationQueuedBeforeStatePersisted = !statePersisted;
                    })
                    .Returns(Task.FromResult(0));

                _validatorStateService
                    .Setup(x => x.TryAddValidatorStatusAsync(
                                    It.IsAny<IValidationRequest>(),
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
                    .Verify(x => x.EnqueueProcessSignatureAsync(It.IsAny<IValidationRequest>(), false), Times.Once);

                _validatorStateService
                    .Verify(
                        x => x.TryAddValidatorStatusAsync(
                                It.IsAny<IValidationRequest>(),
                                It.IsAny<ValidatorStatus>(),
                                It.Is<ValidationStatus>(s => s == ValidationStatus.Incomplete)),
                        Times.Once);

                _telemetryService.Verify(
                    x => x.TrackDurationToStartPackageSigningValidator(It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once);

                Assert.True(verificationQueuedBeforeStatePersisted);
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
                    .Setup(x => x.GetStatusAsync(It.IsAny<IValidationRequest>()))
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
                _blobProvider.Verify(x => x.GetBlobFromUrl (It.IsAny<string>()), Times.Never);
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
            protected readonly Mock<ITelemetryService> _telemetryService;
            protected readonly ILogger<PackageSignatureProcessor> _logger;
            protected readonly Mock<IValidationRequest> _validationRequest;
            protected readonly PackageSignatureProcessor _target;

            public FactsBase(ITestOutputHelper output)
            {
                _validatorStateService = new Mock<IValidatorStateService>();
                _packageSignatureVerifier = new Mock<IProcessSignatureEnqueuer>();
                _blobProvider = new Mock<ISimpleCloudBlobProvider>();
                _telemetryService = new Mock<ITelemetryService>();
                var loggerFactory = new LoggerFactory().AddXunit(output);
                _logger = loggerFactory.CreateLogger<PackageSignatureProcessor>();

                _validationRequest = new Mock<IValidationRequest>();
                _validationRequest.Setup(x => x.NupkgUrl).Returns(NupkgUrl);
                _validationRequest.Setup(x => x.PackageId).Returns(PackageId);
                _validationRequest.Setup(x => x.PackageKey).Returns(PackageKey);
                _validationRequest.Setup(x => x.PackageVersion).Returns(PackageVersion);
                _validationRequest.Setup(x => x.ValidationId).Returns(ValidationId);

                _target = new PackageSignatureProcessor(
                    _validatorStateService.Object,
                    _packageSignatureVerifier.Object,
                    _blobProvider.Object,
                    _telemetryService.Object,
                    _logger);
            }
        }
    }
}
