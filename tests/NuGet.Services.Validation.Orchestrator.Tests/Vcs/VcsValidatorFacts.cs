// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Moq;
using NuGet.Jobs.Validation.Common;
using NuGetGallery;
using NuGet.Services.Validation.Orchestrator;
using Xunit;

namespace NuGet.Services.Validation.Vcs
{
    public class VcsValidatorFacts
    {
        private const int PackageKey = 1001;
        private const string PackageId = "NuGet.Versioning";
        private const string PackageVersion = "4.3.0.0-ALPHA+git";
        private static readonly Guid ValidationId = new Guid("fb9c0bac-3d4d-4cc7-ac2d-b3940e15b94d");
        private const string NupkgUrl = "https://example/nuget.versioning/4.3.0/package.nupkg";

        private const string ValidatorName = "validator-vcs";

        private const string NormalizedPackageId = "nuget.versioning";
        private const string NormalizedPackageVersion = "4.3.0-alpha";

        public class TheGetStatusMethod : FactsBase
        {
            private static readonly ISet<ValidationEvent> IncompleteEvents = new HashSet<ValidationEvent>
            {
                ValidationEvent.ValidatorException,
                ValidationEvent.BeforeVirusScanRequest,
                ValidationEvent.VirusScanRequestSent,
                ValidationEvent.VirusScanRequestFailed,
            };

            private static readonly ISet<ValidationEvent> SucceededEvents = new HashSet<ValidationEvent>
            {
                ValidationEvent.PackageClean,
            };

            private static readonly ISet<ValidationEvent> FailedEvents = new HashSet<ValidationEvent>(
                new[] { (ValidationEvent)(-1) }.Concat(Enum
                    .GetValues(typeof(ValidationEvent))
                    .Cast<ValidationEvent>()
                    .Except(IncompleteEvents)
                    .Except(SucceededEvents)));

            [Fact]
            public async Task ReturnsNotStartedForNullAudit()
            {
                // Arrange & Act
                var actual = await _target.GetResultAsync(_validationRequest.Object);

                // Assert
                Assert.Equal(ValidationStatus.NotStarted, actual.Status);
                _validationAuditor.Verify(
                    x => x.ReadAuditAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once);
                _validationAuditor.Verify(
                    x => x.ReadAuditAsync(ValidationId, NormalizedPackageId, NormalizedPackageVersion),
                    Times.Once);
                _validationService.Verify(
                    x => x.StartValidationProcessAsync(It.IsAny<NuGetPackage>(), It.IsAny<string[]>(), It.IsAny<Guid>()),
                    Times.Never);
            }

            [Theory]
            [MemberData(nameof(FailedTestData))]
            public async Task ReturnsFailedIfAuditHasAnyFailedEvents(ValidationEvent validationEvent)
            {
                // Arrange
                _validationAuditor
                    .Setup(x => x.ReadAuditAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new PackageValidationAudit
                    {
                        Entries = new List<PackageValidationAuditEntry>
                        {
                            new PackageValidationAuditEntry
                            {
                                ValidatorName = ValidatorName,
                                EventId = IncompleteEvents.First(),
                            },
                            new PackageValidationAuditEntry
                            {
                                ValidatorName = ValidatorName,
                                EventId = SucceededEvents.First(),
                            },
                            new PackageValidationAuditEntry
                            {
                                ValidatorName = ValidatorName,
                                EventId = validationEvent,
                            },
                        },
                    });

                // Act
                var actual = await _target.GetResultAsync(_validationRequest.Object);

                // Assert
                Assert.Equal(ValidationStatus.Failed, actual.Status);
                _validationAuditor.Verify(
                    x => x.ReadAuditAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once);
                _validationAuditor.Verify(
                    x => x.ReadAuditAsync(ValidationId, NormalizedPackageId, NormalizedPackageVersion),
                    Times.Once);
                _validationService.Verify(
                    x => x.StartValidationProcessAsync(It.IsAny<NuGetPackage>(), It.IsAny<string[]>(), It.IsAny<Guid>()),
                    Times.Never);
            }

            [Theory]
            [MemberData(nameof(SucceededTestData))]
            public async Task ReturnsSucceededIfAuditHasNoFailedEvents(ValidationEvent validationEvent)
            {
                // Arrange
                _validationAuditor
                    .Setup(x => x.ReadAuditAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new PackageValidationAudit
                    {
                        Entries = new List<PackageValidationAuditEntry>
                        {
                            new PackageValidationAuditEntry
                            {
                                ValidatorName = ValidatorName,
                                EventId = IncompleteEvents.First(),
                            },
                            new PackageValidationAuditEntry
                            {
                                ValidatorName = ValidatorName,
                                EventId = validationEvent,
                            },
                        },
                    });

                // Act
                var actual = await _target.GetResultAsync(_validationRequest.Object);

                // Assert
                Assert.Equal(ValidationStatus.Succeeded, actual.Status);
                _validationAuditor.Verify(
                    x => x.ReadAuditAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once);
                _validationAuditor.Verify(
                    x => x.ReadAuditAsync(ValidationId, NormalizedPackageId, NormalizedPackageVersion),
                    Times.Once);
                _validationService.Verify(
                    x => x.StartValidationProcessAsync(It.IsAny<NuGetPackage>(), It.IsAny<string[]>(), It.IsAny<Guid>()),
                    Times.Never);
            }

            [Theory]
            [MemberData(nameof(IncompleteTestData))]
            public async Task ReturnsIncompleteIfAuditHasNoFailedOrSucceededEvents(ValidationEvent validationEvent)
            {
                // Arrange
                _validationAuditor
                    .Setup(x => x.ReadAuditAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new PackageValidationAudit
                    {
                        Entries = new List<PackageValidationAuditEntry>
                        {
                            new PackageValidationAuditEntry
                            {
                                ValidatorName = ValidatorName,
                                EventId = validationEvent,
                            },
                        },
                    });

                // Act
                var actual = await _target.GetResultAsync(_validationRequest.Object);

                // Assert
                Assert.Equal(ValidationStatus.Incomplete, actual.Status);
                _validationAuditor.Verify(
                    x => x.ReadAuditAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once);
                _validationAuditor.Verify(
                    x => x.ReadAuditAsync(ValidationId, NormalizedPackageId, NormalizedPackageVersion),
                    Times.Once);
                _validationService.Verify(
                    x => x.StartValidationProcessAsync(It.IsAny<NuGetPackage>(), It.IsAny<string[]>(), It.IsAny<Guid>()),
                    Times.Never);
            }

            [Fact]
            public async Task ReturnsIncompleteIfAuditHasEventsWithCorrectValidatorName()
            {
                // Arrange
                var someOtherValidatorName = "some-other-validator";
                _validationAuditor
                    .Setup(x => x.ReadAuditAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new PackageValidationAudit
                    {
                        Entries = new List<PackageValidationAuditEntry>
                        {
                            new PackageValidationAuditEntry
                            {
                                ValidatorName = someOtherValidatorName,
                                EventId = ValidationEvent.ScanFailed,
                            },
                            new PackageValidationAuditEntry
                            {
                                ValidatorName = someOtherValidatorName,
#pragma warning disable CS0612 // Type or member is obsolete
                                EventId = ValidationEvent.UnzipSucceeeded,
#pragma warning restore CS0612 // Type or member is obsolete
                            },
                            new PackageValidationAuditEntry
                            {
                                ValidatorName = someOtherValidatorName,
                                EventId = ValidationEvent.BeforeVirusScanRequest,
                            },
                        },
                    });

                // Act
                var actual = await _target.GetResultAsync(_validationRequest.Object);

                // Assert
                Assert.Equal(ValidationStatus.Incomplete, actual.Status);
                _validationAuditor.Verify(
                    x => x.ReadAuditAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once);
                _validationAuditor.Verify(
                    x => x.ReadAuditAsync(ValidationId, NormalizedPackageId, NormalizedPackageVersion),
                    Times.Once);
                _validationService.Verify(
                    x => x.StartValidationProcessAsync(It.IsAny<NuGetPackage>(), It.IsAny<string[]>(), It.IsAny<Guid>()),
                    Times.Never);
            }

            [Fact]
            public async Task ReturnsSuccessIfPackageIsExcludedByCriteria()
            {
                // Arrange
                _criteriaEvaluator
                    .Setup(x => x.IsMatch(It.IsAny<ICriteria>(), It.IsAny<Package>()))
                    .Returns(false);
                
                // Act
                var actual = await _target.GetResultAsync(_validationRequest.Object);

                // Assert
                Assert.Equal(ValidationStatus.Succeeded, actual.Status);
                _packageService.Verify(
                    x => x.FindPackageByIdAndVersionStrict(PackageId, PackageVersion),
                    Times.Once);
                _criteriaEvaluator.Verify(
                    x => x.IsMatch(It.IsAny<ICriteria>(), It.IsAny<Package>()),
                    Times.Once);
                _validationAuditor.Verify(
                    x => x.ReadAuditAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()),
                    Times.Never);
            }

            public static IEnumerable<object[]> IncompleteTestData => IncompleteEvents.Select(e => new object[] { e });
            public static IEnumerable<object[]> SucceededTestData => SucceededEvents.Select(e => new object[] { e });
            public static IEnumerable<object[]> FailedTestData => FailedEvents.Select(e => new object[] { e });
        }

        public class TheStartValidationMethod : FactsBase
        {
            private readonly IList<StartedValidation> _started = new List<StartedValidation>();

            public TheStartValidationMethod()
            {
                _validationService
                    .Setup(x => x.StartValidationProcessAsync(It.IsAny<NuGetPackage>(), It.IsAny<string[]>(), It.IsAny<Guid>()))
                    .Returns(Task.FromResult(0))
                    .Callback<NuGetPackage, string[], Guid>((p, v, i) => _started.Add(new StartedValidation(p, v, i)));

                _validationAuditor
                    .Setup(x => x.ReadAuditAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new PackageValidationAudit());
            }

            [Fact]
            public async Task UsesTheCorrectPackageForValidation()
            {
                // Arrange & Act
                var status = await _target.StartAsync(_validationRequest.Object);

                // Assert
                Assert.Equal(1, _started.Count);
                var started = _started[0];
                Assert.Equal(ValidationId, started.ValidationId);
                Assert.NotNull(started.Package);
                Assert.Equal(NormalizedPackageId, started.Package.Id);
                Assert.Equal(NormalizedPackageVersion, started.Package.Version);
                Assert.Equal(NormalizedPackageVersion, started.Package.NormalizedVersion);
                Assert.Equal(NupkgUrl, started.Package.DownloadUrl.ToString());
                Assert.Equal(new[] { ValidatorName }, started.Validators);
                Assert.Equal(ValidationStatus.Incomplete, status.Status);
                _validationService.Verify(
                    x => x.StartValidationProcessAsync(
                        It.IsAny<NuGetPackage>(),
                        It.IsAny<string[]>(),
                        It.IsAny<Guid>()),
                    Times.Once);
                _validationAuditor.Verify(
                    x => x.ReadAuditAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once);
            }

            [Theory]
            [InlineData(HttpStatusCode.Conflict)]
            [InlineData(HttpStatusCode.PreconditionFailed)]
            public async Task IgnoresExceptionsFromAlreadyStartedValidation(HttpStatusCode statusCode)
            {
                // Arrange
                _validationService
                    .Setup(x => x.StartValidationProcessAsync(It.IsAny<NuGetPackage>(), It.IsAny<string[]>(), It.IsAny<Guid>()))
                    .Throws(new StorageException(
                        new RequestResult { HttpStatusCode = (int)statusCode },
                        "Storage exception",
                        inner: null));

                // Act
                var result = await _target.StartAsync(_validationRequest.Object);

                // Assert
                Assert.Equal(ValidationStatus.Incomplete, result.Status);
                _validationService.Verify(
                    x => x.StartValidationProcessAsync(
                        It.IsAny<NuGetPackage>(),
                        It.IsAny<string[]>(),
                        It.IsAny<Guid>()),
                    Times.Once);
                _validationAuditor.Verify(
                    x => x.ReadAuditAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once);
            }

            [Fact]
            public async Task DoesNotSwallowUnexpectedExceptions()
            {
                // Arrange
                var expected = new FormatException("Something!");
                _validationService
                    .Setup(x => x.StartValidationProcessAsync(It.IsAny<NuGetPackage>(), It.IsAny<string[]>(), It.IsAny<Guid>()))
                    .Throws(expected);

                // Act & Assert
                var actual = await Assert.ThrowsAsync(
                    expected.GetType(),
                    () => _target.StartAsync(_validationRequest.Object));
                Assert.Same(expected, actual);            

                _validationService.Verify(
                    x => x.StartValidationProcessAsync(
                        It.IsAny<NuGetPackage>(),
                        It.IsAny<string[]>(),
                        It.IsAny<Guid>()),
                    Times.Once);
                _validationAuditor.Verify(
                    x => x.ReadAuditAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()),
                    Times.Never);
            }

            [Fact]
            public async Task ReturnsSuccessIfPackageIsExcludedByCriteria()
            {
                // Arrange
                _criteriaEvaluator
                    .Setup(x => x.IsMatch(It.IsAny<ICriteria>(), It.IsAny<Package>()))
                    .Returns(false);

                // Act
                var actual = await _target.StartAsync(_validationRequest.Object);

                // Assert
                Assert.Equal(ValidationStatus.Succeeded, actual.Status);
                _packageService.Verify(
                    x => x.FindPackageByIdAndVersionStrict(PackageId, PackageVersion),
                    Times.Once);
                _criteriaEvaluator.Verify(
                    x => x.IsMatch(It.IsAny<ICriteria>(), It.IsAny<Package>()),
                    Times.Once);
                _validationAuditor.Verify(
                    x => x.ReadAuditAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()),
                    Times.Never);
                _validationService.Verify(
                    x => x.StartValidationProcessAsync(It.IsAny<NuGetPackage>(), It.IsAny<string[]>(), It.IsAny<Guid>()),
                    Times.Never);
            }

            private class StartedValidation
            {
                public StartedValidation(NuGetPackage package, string[] validators, Guid validationId)
                {
                    Package = package;
                    Validators = validators;
                    ValidationId = validationId;
                }

                public NuGetPackage Package { get; }
                public string[] Validators { get; }
                public Guid ValidationId { get; }
            }
        }

        public abstract class FactsBase
        {
            protected readonly VcsConfiguration _config;
            protected readonly Mock<IValidationRequest> _validationRequest;
            protected readonly Mock<IPackageValidationService> _validationService;
            protected readonly Mock<IPackageValidationAuditor> _validationAuditor;
            protected readonly Mock<IEntityService<Package>> _packageService;
            protected readonly Mock<ICriteriaEvaluator<Package>> _criteriaEvaluator;
            protected readonly Mock<IOptionsSnapshot<VcsConfiguration>> _options;
            protected readonly Mock<ILogger<VcsValidator>> _logger;
            protected readonly VcsValidator _target;

            public FactsBase()
            {
                _config = new VcsConfiguration();
                _validationRequest = new Mock<IValidationRequest>();
                _validationRequest.Setup(x => x.NupkgUrl).Returns(NupkgUrl);
                _validationRequest.Setup(x => x.PackageId).Returns(PackageId);
                _validationRequest.Setup(x => x.PackageKey).Returns(PackageKey);
                _validationRequest.Setup(x => x.PackageVersion).Returns(PackageVersion);
                _validationRequest.Setup(x => x.ValidationId).Returns(ValidationId);

                _validationService = new Mock<IPackageValidationService>();
                _validationAuditor = new Mock<IPackageValidationAuditor>();
                _packageService = new Mock<IEntityService<Package>>();
                _criteriaEvaluator = new Mock<ICriteriaEvaluator<Package>>();
                _options = new Mock<IOptionsSnapshot<VcsConfiguration>>();
                _logger = new Mock<ILogger<VcsValidator>>();

                _criteriaEvaluator
                    .Setup(x => x.IsMatch(It.IsAny<ICriteria>(), It.IsAny<Package>()))
                    .Returns(true);

                _options
                    .Setup(x => x.Value)
                    .Returns(() => _config);

                _target = new VcsValidator(
                    _validationService.Object,
                    _validationAuditor.Object,
                    _packageService.Object,
                    _criteriaEvaluator.Object,
                    _options.Object,
                    _logger.Object);
            }
        }
    }
}
