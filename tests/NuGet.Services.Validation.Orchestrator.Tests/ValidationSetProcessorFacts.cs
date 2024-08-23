﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Issues;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class StrictMockValidationSetProcessorFacts : ValidationSetProcessorFactsBase
    {
        public StrictMockValidationSetProcessorFacts()
            : base(validationStorageMockBehavior: MockBehavior.Strict)
        {
        }

        [Fact]
        public async Task FailsUnknownValidations()
        {
            AddValidationToSet("validation1");
            ValidationStorageMock
                .Setup(vs => vs.UpdateValidationStatusAsync(ValidationSet.PackageValidations.First(), NuGetValidationResponse.Failed))
                .Returns(Task.FromResult(0))
                .Verifiable();

            var processor = CreateProcessor();
            await processor.ProcessValidationsAsync(ValidationSet);

            ValidationStorageMock
                .Verify(vs => vs.UpdateValidationStatusAsync(ValidationSet.PackageValidations.First(), NuGetValidationResponse.Failed), Times.Once());
        }
    }

    public class DefaultMockValidationSetProcessorFacts : ValidationSetProcessorFactsBase
    {
        [Theory]
        [InlineData(ValidationStatus.NotStarted, false, false)]
        [InlineData(ValidationStatus.Incomplete, true, false)]
        [InlineData(ValidationStatus.Succeeded, true, true)]
        [InlineData(ValidationStatus.Failed, true, true)]
        public async Task StartsNotStartedValidations(ValidationStatus startStatus, bool expectStorageUpdate, bool expectCleanup)
        {
            UseDefaultValidatorProvider();
            const string validationName = "validation1";
            var validator = AddValidation(validationName, TimeSpan.FromDays(1));
            var validation = ValidationSet.PackageValidations.First();

            ValidationStatus actualStatus = ValidationStatus.NotStarted;
            validator
                .Setup(v => v.GetResponseAsync(It.IsAny<INuGetValidationRequest>()))
                .ReturnsAsync(new NuGetValidationResponse(actualStatus))
                .Callback<INuGetValidationRequest>(r => Assert.Equal(validation.Key, r.ValidationId));

            validator
                .Setup(v => v.StartAsync(It.IsAny<INuGetValidationRequest>()))
                .ReturnsAsync(new NuGetValidationResponse(startStatus))
                .Callback<INuGetValidationRequest>(r => {
                    Assert.Equal(validation.Key, r.ValidationId);
                    actualStatus = startStatus;
                })
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.MarkValidationStartedAsync(validation, It.Is<NuGetValidationResponse>(r => r.Status == startStatus)))
                .Returns(Task.FromResult(0))
                .Callback<PackageValidation, INuGetValidationResponse>((pv, vr) => pv.ValidationStatus = vr.Status)
                .Verifiable();

            var processor = CreateProcessor();
            await processor.ProcessValidationsAsync(ValidationSet);

            validator.Verify(v => v.StartAsync(It.IsAny<INuGetValidationRequest>()), Times.AtLeastOnce());
            if (expectStorageUpdate)
            {
                ValidationStorageMock.Verify(
                    vs => vs.MarkValidationStartedAsync(validation, It.Is<NuGetValidationResponse>(r => r.Status == startStatus)), Times.Once);
                ValidationStorageMock.Verify(
                    vs => vs.MarkValidationStartedAsync(It.IsAny<PackageValidation>(), It.IsAny<NuGetValidationResponse>()), Times.Once);
                TelemetryServiceMock.Verify(
                    ts => ts.TrackValidatorStarted(ValidationSet.PackageId, ValidationSet.PackageNormalizedVersion, ValidationSet.ValidationTrackingId, validationName), Times.Once);
                TelemetryServiceMock.Verify(
                    ts => ts.TrackValidatorStarted(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Once);
            }
            else
            {
                ValidationStorageMock.Verify(
                    vs => vs.MarkValidationStartedAsync(It.IsAny<PackageValidation>(), It.IsAny<NuGetValidationResponse>()), Times.Never);
                TelemetryServiceMock.Verify(
                    ts => ts.TrackValidatorStarted(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
            }

            if (expectCleanup)
            {
                validator.Verify(
                    x => x.CleanUpAsync(It.IsAny<INuGetValidationRequest>()),
                    Times.Once);
            }
            else
            {
                validator.Verify(
                    x => x.CleanUpAsync(It.IsAny<INuGetValidationRequest>()),
                    Times.Never);
            }
        }

        [Fact]
        public async Task DoesNotStartValidationWithUnmetPrerequisites()
        {
            UseDefaultValidatorProvider();
            const string validation1 = "validation1";
            const string validation2 = "validation2";
            var validator1 = AddValidation(validation1, TimeSpan.FromDays(1), validationStatus: ValidationStatus.Incomplete);
            var validator2 = AddValidation(validation2, TimeSpan.FromDays(1), requiredValidations: new[] { validation1 });

            validator1
                .Setup(v => v.GetResponseAsync(It.IsAny<INuGetValidationRequest>()))
                .ReturnsAsync(NuGetValidationResponse.Incomplete);

            validator2
                .Setup(v => v.GetResponseAsync(It.IsAny<INuGetValidationRequest>()))
                .ReturnsAsync(NuGetValidationResponse.NotStarted);

            validator2
                .Setup(v => v.StartAsync(It.IsAny<INuGetValidationRequest>()))
                .ReturnsAsync(NuGetValidationResponse.Incomplete)
                .Verifiable();

            var processor = CreateProcessor();
            await processor.ProcessValidationsAsync(ValidationSet);

            validator1.Verify(v => v.StartAsync(It.IsAny<INuGetValidationRequest>()), Times.Never);
            validator1.Verify(v => v.GetResponseAsync(It.IsAny<INuGetValidationRequest>()), Times.Once);
            validator1.Verify(v => v.CleanUpAsync(It.IsAny<INuGetValidationRequest>()), Times.Never);
            validator2.Verify(v => v.StartAsync(It.IsAny<INuGetValidationRequest>()), Times.Never);
            validator2.Verify(v => v.GetResponseAsync(It.IsAny<INuGetValidationRequest>()), Times.Never);
            validator2.Verify(v => v.CleanUpAsync(It.IsAny<INuGetValidationRequest>()), Times.Never);
        }

        [Fact]
        public async Task DoesNotStartDisabledValidations()
        {
            UseDefaultValidatorProvider();
            const string validation1 = "validation1";
            var validator = AddValidation(validation1, TimeSpan.FromDays(1), validationStatus: ValidationStatus.Incomplete, shouldStart: false);
            validator
                .Setup(v => v.GetResponseAsync(It.IsAny<INuGetValidationRequest>()))
                .ReturnsAsync(NuGetValidationResponse.Incomplete);

            var processor = CreateProcessor();
            await processor.ProcessValidationsAsync(ValidationSet);

            validator
                .Verify(v => v.StartAsync(It.IsAny<INuGetValidationRequest>()), Times.Never());
        }

        [Theory]
        [InlineData(ValidationStatus.Incomplete, true, ValidationFailureBehavior.MustSucceed, false)]
        [InlineData(ValidationStatus.Succeeded, true, ValidationFailureBehavior.MustSucceed, true)]
        [InlineData(ValidationStatus.Failed, true, ValidationFailureBehavior.MustSucceed, true)]
        [InlineData(ValidationStatus.Incomplete, false, ValidationFailureBehavior.MustSucceed, false)]
        [InlineData(ValidationStatus.Succeeded, false, ValidationFailureBehavior.MustSucceed, true)]
        [InlineData(ValidationStatus.Failed, false, ValidationFailureBehavior.MustSucceed, true)]
        [InlineData(ValidationStatus.Incomplete, true, ValidationFailureBehavior.AllowedToFail, false)]
        [InlineData(ValidationStatus.Succeeded, true, ValidationFailureBehavior.AllowedToFail, true)]
        [InlineData(ValidationStatus.Failed, true, ValidationFailureBehavior.AllowedToFail, true)]
        [InlineData(ValidationStatus.Incomplete, false, ValidationFailureBehavior.AllowedToFail, false)]
        [InlineData(ValidationStatus.Succeeded, false, ValidationFailureBehavior.AllowedToFail, true)]
        [InlineData(ValidationStatus.Failed, false, ValidationFailureBehavior.AllowedToFail, true)]
        public async Task HandlesIncompleteValidationsStatusChanges(ValidationStatus targetStatus, bool shouldStart, ValidationFailureBehavior failureBehavior, bool expectStorageUpdate)
        {
            UseDefaultValidatorProvider();
            var validator = AddValidation("validation1", TimeSpan.FromDays(1), validationStatus: ValidationStatus.Incomplete, shouldStart: shouldStart, failureBehavior: failureBehavior);
            var validation = ValidationSet.PackageValidations.First();

            validator
                .Setup(v => v.GetResponseAsync(It.IsAny<INuGetValidationRequest>()))
                .ReturnsAsync(new NuGetValidationResponse(targetStatus))
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.UpdateValidationStatusAsync(validation, It.Is<INuGetValidationResponse>(r => r.Status == targetStatus)))
                .Returns(Task.FromResult(0))
                .Callback<PackageValidation, INuGetValidationResponse>((pv, vr) => pv.ValidationStatus = vr.Status)
                .Verifiable();

            var processor = CreateProcessor();
            await processor.ProcessValidationsAsync(ValidationSet);

            validator
                .Verify(v => v.GetResponseAsync(It.IsAny<INuGetValidationRequest>()), Times.AtLeastOnce());
            if (expectStorageUpdate)
            {
                ValidationStorageMock.Verify(
                    vs => vs.UpdateValidationStatusAsync(validation, It.Is<INuGetValidationResponse>(r => r.Status == targetStatus)), Times.Once());
                ValidationStorageMock.Verify(
                    vs => vs.UpdateValidationStatusAsync(It.IsAny<PackageValidation>(), It.IsAny<NuGetValidationResponse>()), Times.Once());
                validator.Verify(
                    x => x.CleanUpAsync(It.IsAny<INuGetValidationRequest>()), Times.Once);
            }
            else
            {
                ValidationStorageMock.Verify(
                    vs => vs.UpdateValidationStatusAsync(It.IsAny<PackageValidation>(), It.IsAny<NuGetValidationResponse>()), Times.Never());
                validator.Verify(
                    x => x.CleanUpAsync(It.IsAny<INuGetValidationRequest>()), Times.Never);
            }
            Assert.Equal(targetStatus, validation.ValidationStatus);
        }

        [Theory]
        [InlineData(ValidationStatus.Failed)]
        [InlineData(ValidationStatus.Succeeded)]
        public async Task HandlesTerminalStatusOnStart(ValidationStatus targetStatus)
        {
            UseDefaultValidatorProvider();
            var validator = AddValidation("validation1", TimeSpan.FromDays(1), validationStatus: ValidationStatus.NotStarted);
            var validation = ValidationSet.PackageValidations.First();

            var validationResponse = new NuGetValidationResponse(targetStatus, new List<IValidationIssue>
            {
                ValidationIssue.PackageIsSigned,
            });

            validator
                .Setup(v => v.GetResponseAsync(It.IsAny<INuGetValidationRequest>()))
                .ReturnsAsync(validationResponse)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.MarkValidationStartedAsync(
                                validation,
                                It.Is<INuGetValidationResponse>(r => r.Status == targetStatus && r.Issues.Any())))
                .Callback<PackageValidation, INuGetValidationResponse>((v, vr) => v.ValidationStatus = vr.Status)
                .Returns(Task.FromResult(0));

            var processor = CreateProcessor();
            await processor.ProcessValidationsAsync(ValidationSet);

            ValidationStorageMock
                .Verify(vs => vs.MarkValidationStartedAsync(
                                validation,
                                It.Is<INuGetValidationResponse>(r => r.Status == targetStatus && r.Issues.Any())),
                    Times.Once());
            validator.Verify(
                x => x.CleanUpAsync(It.Is<INuGetValidationRequest>(y => y != null)),
                Times.Once);
        }

        [Theory]
        [InlineData(ValidationStatus.Failed)]
        [InlineData(ValidationStatus.Succeeded)]
        public async Task HandlesTerminalStatusOnUpdate(ValidationStatus targetStatus)
        {
            UseDefaultValidatorProvider();
            var validator = AddValidation("validation1", TimeSpan.FromDays(1), validationStatus: ValidationStatus.Incomplete);
            var validation = ValidationSet.PackageValidations.First();

            var validationResponse = new NuGetValidationResponse(targetStatus, new List<IValidationIssue>
            {
                ValidationIssue.PackageIsSigned,
            });

            validator
                .Setup(v => v.GetResponseAsync(It.IsAny<INuGetValidationRequest>()))
                .ReturnsAsync(validationResponse)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.UpdateValidationStatusAsync(
                                validation,
                                It.Is<INuGetValidationResponse>(r => r.Status == targetStatus && r.Issues.Any())))
                .Returns(Task.FromResult(0));

            var processor = CreateProcessor();
            await processor.ProcessValidationsAsync(ValidationSet);

            ValidationStorageMock
                .Verify(vs => vs.UpdateValidationStatusAsync(
                                validation,
                                It.Is<INuGetValidationResponse>(r => r.Status == targetStatus && r.Issues.Any())),
                    Times.Once());
            validator.Verify(
                x => x.CleanUpAsync(It.Is<INuGetValidationRequest>(y => y != null)),
                Times.Once);
        }

        [Fact]
        public async Task UsesProperNupkgUrl()
        {
            UseDefaultValidatorProvider();
            var validator = AddValidation("validation1", TimeSpan.FromDays(1));

            INuGetValidationRequest validationRequest = null;
            validator
                .Setup(v => v.GetResponseAsync(It.IsAny<INuGetValidationRequest>()))
                .ReturnsAsync(NuGetValidationResponse.NotStarted)
                .Callback<INuGetValidationRequest>(vr => validationRequest = vr);
            validator
                .Setup(v => v.StartAsync(It.IsAny<INuGetValidationRequest>()))
                .ReturnsAsync(NuGetValidationResponse.Incomplete);

            var processor = CreateProcessor();
            var expectedEndOfAccessLower = DateTimeOffset.UtcNow.Add(Configuration.TimeoutValidationSetAfter);

            await processor.ProcessValidationsAsync(ValidationSet);

            var expectedEndOfAccessUpper = DateTimeOffset.UtcNow.Add(Configuration.TimeoutValidationSetAfter);

            validator
                .Verify(v => v.GetResponseAsync(It.IsAny<INuGetValidationRequest>()), Times.AtLeastOnce());
            PackageFileServiceMock
                .Verify(s =>
                    s.GetPackageForValidationSetReadUriAsync(
                        ValidationSet,
                        null,
                        It.Is<DateTimeOffset>(actualEndOfAccess => actualEndOfAccess >= expectedEndOfAccessLower && actualEndOfAccess <= expectedEndOfAccessUpper)),
                    Times.Once);
            Assert.NotNull(validationRequest);
            Assert.Contains(ValidationSet.ValidationTrackingId.ToString(), validationRequest.NupkgUrl);
            Assert.Contains(ValidationContainerName, validationRequest.NupkgUrl);
            Assert.Equal(Package.PackageRegistration.Id, validationRequest.PackageId);
            Assert.Equal(Package.NormalizedVersion, validationRequest.PackageVersion);
        }

        [Fact]
        public async Task UsesProperNupkgUrlWithSasDefinition()
        {
            UseDefaultValidatorProvider();
            SasDefinitionConfiguration.ValidationSetProcessorSasDefinition = "ValidationSetProcessorSasDefinition";
            var validator = AddValidation("validation1", TimeSpan.FromDays(1));

            INuGetValidationRequest validationRequest = null;
            validator
                .Setup(v => v.GetResponseAsync(It.IsAny<INuGetValidationRequest>()))
                .ReturnsAsync(NuGetValidationResponse.NotStarted)
                .Callback<INuGetValidationRequest>(vr => validationRequest = vr);
            validator
                .Setup(v => v.StartAsync(It.IsAny<INuGetValidationRequest>()))
                .ReturnsAsync(NuGetValidationResponse.Incomplete);

            var processor = CreateProcessor();
            var expectedEndOfAccessLower = DateTimeOffset.UtcNow.Add(Configuration.TimeoutValidationSetAfter);

            await processor.ProcessValidationsAsync(ValidationSet);

            var expectedEndOfAccessUpper = DateTimeOffset.UtcNow.Add(Configuration.TimeoutValidationSetAfter);

            validator
                .Verify(v => v.GetResponseAsync(It.IsAny<INuGetValidationRequest>()), Times.AtLeastOnce());
            PackageFileServiceMock
                .Verify(s =>
                    s.GetPackageForValidationSetReadUriAsync(
                        ValidationSet,
                        SasDefinitionConfiguration.ValidationSetProcessorSasDefinition,
                        It.IsAny<DateTimeOffset>()),
                    Times.Once);
            Assert.NotNull(validationRequest);
            Assert.Contains(ValidationSet.ValidationTrackingId.ToString(), validationRequest.NupkgUrl);
            Assert.Contains(ValidationContainerName, validationRequest.NupkgUrl);
            Assert.Equal(Package.PackageRegistration.Id, validationRequest.PackageId);
            Assert.Equal(Package.NormalizedVersion, validationRequest.PackageVersion);
        }

        private class TestException : Exception { };

        [Fact]
        public async Task IgnoresExceptionsForAllowedToFailValidations()
        {
            UseDefaultValidatorProvider();
            var validatorMock = AddValidation("throwingValidation", TimeSpan.FromDays(1), failureBehavior: ValidationFailureBehavior.AllowedToFail);
            validatorMock
                .Setup(v => v.StartAsync(It.IsAny<INuGetValidationRequest>()))
                .ThrowsAsync(new TestException());
            validatorMock
                .Setup(v => v.GetResponseAsync(It.IsAny<INuGetValidationRequest>()))
                .ReturnsAsync(NuGetValidationResponse.NotStarted);

            var processor = CreateProcessor();
            var ex = await Record.ExceptionAsync(() => processor.ProcessValidationsAsync(ValidationSet));

            Assert.Null(ex);
        }

        [Fact]
        public async Task RethrowsExceptionForMustSucceedValidations()
        {
            UseDefaultValidatorProvider();
            var validatorMock = AddValidation("throwingValidation", TimeSpan.FromDays(1), failureBehavior: ValidationFailureBehavior.MustSucceed);
            validatorMock
                .Setup(v => v.StartAsync(It.IsAny<INuGetValidationRequest>()))
                .ThrowsAsync(new TestException());
            validatorMock
                .Setup(v => v.GetResponseAsync(It.IsAny<INuGetValidationRequest>()))
                .ReturnsAsync(NuGetValidationResponse.NotStarted);

            var processor = CreateProcessor();
            await Assert.ThrowsAsync<TestException>(() => processor.ProcessValidationsAsync(ValidationSet));
        }
    }

    public abstract class ValidationSetProcessorFactsBase
    {
        protected const string PublicContainerName = "packages-container";
        protected const string ValidationContainerName = "validation-container";

        protected Mock<IValidatorProvider> ValidatorProviderMock { get; }
        protected Mock<IValidationStorageService> ValidationStorageMock { get; }
        protected Mock<IOptionsSnapshot<ValidationConfiguration>> ConfigurationAccessorMock { get; }
        protected Mock<IOptionsSnapshot<SasDefinitionConfiguration>> SasDefinitionConfigurationAccessorMock { get; }
        protected Mock<IValidationFileService> PackageFileServiceMock { get; }
        protected Mock<ILogger<ValidationSetProcessor>> LoggerMock { get; }
        public Mock<ITelemetryService> TelemetryServiceMock { get; }
        protected ValidationConfiguration Configuration { get; }
        protected SasDefinitionConfiguration SasDefinitionConfiguration { get; }
        protected Package Package { get; }
        protected PackageValidationSet ValidationSet { get; }
        protected Dictionary<string, Mock<INuGetValidator>> Validators { get; }

        protected ValidationSetProcessorFactsBase(
            MockBehavior validatorProviderMockBehavior = MockBehavior.Default,
            MockBehavior validationStorageMockBehavior = MockBehavior.Default,
            MockBehavior configurationAccessorMockBehavior = MockBehavior.Default,
            MockBehavior sasDefinitionConfigurationAccessorMockBehavior = MockBehavior.Default,
            MockBehavior packageFileServiceMockBehavior = MockBehavior.Default,
            MockBehavior telemetryServiceMockBehavior = MockBehavior.Default,
            MockBehavior loggerMockBehavior = MockBehavior.Default)
        {
            ValidatorProviderMock = new Mock<IValidatorProvider>(validatorProviderMockBehavior);
            ValidationStorageMock = new Mock<IValidationStorageService>(validationStorageMockBehavior);
            ConfigurationAccessorMock = new Mock<IOptionsSnapshot<ValidationConfiguration>>(configurationAccessorMockBehavior);
            SasDefinitionConfigurationAccessorMock = new Mock<IOptionsSnapshot<SasDefinitionConfiguration>>(sasDefinitionConfigurationAccessorMockBehavior);
            PackageFileServiceMock = new Mock<IValidationFileService>(packageFileServiceMockBehavior);
            LoggerMock = new Mock<ILogger<ValidationSetProcessor>>(loggerMockBehavior);
            TelemetryServiceMock = new Mock<ITelemetryService>(telemetryServiceMockBehavior);
            Configuration = new ValidationConfiguration
            {
                Validations = new List<ValidationConfigurationItem>
                {
                },
                TimeoutValidationSetAfter = TimeSpan.FromDays(5),
            };
            ConfigurationAccessorMock
                .SetupGet(ca => ca.Value)
                .Returns(Configuration);

            SasDefinitionConfiguration = new SasDefinitionConfiguration();
            SasDefinitionConfigurationAccessorMock
                .SetupGet(sca => sca.Value)
                .Returns(SasDefinitionConfiguration);

            Package = new Package
            {
                PackageRegistration = new PackageRegistration { Id = "packageId" },
                Version = "1.2.3.456",
                NormalizedVersion = "1.2.3"
            };

            Package.PackageRegistration.Packages.Add(Package);

            ValidationSet = new PackageValidationSet
            {
                Key = 238423,
                PackageKey = 9999,
                PackageId = Package.PackageRegistration.Id,
                PackageNormalizedVersion = Package.NormalizedVersion,
                PackageValidations = new List<PackageValidation>
                {
                }
            };
            ValidationSet.PackageValidations.ToList().ForEach(v => v.PackageValidationSet = ValidationSet);
            Validators = new Dictionary<string, Mock<INuGetValidator>>();

            PackageFileServiceMock
                .Setup(pfs => pfs.GetPackageForValidationSetReadUriAsync(It.IsAny<PackageValidationSet>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
                .Returns<PackageValidationSet, string, DateTimeOffset>(
                    (p, s, e) => Task.FromResult(new Uri($"https://example.com/{ValidationContainerName}/{p.ValidationTrackingId}/{p.PackageId}.{p.PackageNormalizedVersion}?e={e:yyyy-MM-dd-hh-mm-ss}")));
        }

        protected ValidationSetProcessor CreateProcessor()
            => new ValidationSetProcessor(
                ValidatorProviderMock.Object,
                ValidationStorageMock.Object,
                ConfigurationAccessorMock.Object,
                SasDefinitionConfigurationAccessorMock.Object,
                PackageFileServiceMock.Object,
                TelemetryServiceMock.Object,
                LoggerMock.Object);

        protected PackageValidation AddValidationToSet(
            string type,
            ValidationStatus validationStatus = ValidationStatus.NotStarted,
            DateTime? startTime = null)
        {
            var validation = new PackageValidation
            {
                Type = type,
                PackageValidationSet = ValidationSet,
                Key = Guid.NewGuid(),
                ValidationStatus = validationStatus,
                PackageValidationSetKey = ValidationSet.Key,
                ValidationStatusTimestamp = DateTime.UtcNow,
                Started = startTime.HasValue ? startTime : (validationStatus == ValidationStatus.NotStarted ? (DateTime?)null : DateTime.UtcNow)
            };
            ValidationSet.PackageValidations.Add(validation);
            return validation;
        }

        protected ValidationConfigurationItem AddValidationToConfiguration(string name, TimeSpan failAfter, bool shouldStart, ValidationFailureBehavior failureBehavior, params string[] requiredValidations)
        {
            var validation = new ValidationConfigurationItem
            {
                Name = name,
                TrackAfter = failAfter,
                RequiredValidations = requiredValidations.ToList(),
                ShouldStart = shouldStart,
                FailureBehavior = failureBehavior
            };
            Configuration.Validations.Add(validation);
            return validation;
        }

        protected void UseDefaultValidatorProvider()
        {
            ValidatorProviderMock
                .Setup(vp => vp.GetNuGetValidator(It.IsAny<string>()))
                .Returns<string>(name => Validators[name].Object);
        }

        protected Mock<INuGetValidator> AddValidation(
            string name,
            TimeSpan failAfter,
            string[] requiredValidations = null,
            ValidationStatus validationStatus = ValidationStatus.NotStarted,
            bool shouldStart = true,
            ValidationFailureBehavior failureBehavior = ValidationFailureBehavior.MustSucceed)
        {
            requiredValidations = requiredValidations ?? new string[0];
            AddValidationToSet(name, validationStatus);
            AddValidationToConfiguration(name, failAfter, shouldStart, failureBehavior, requiredValidations);

            var validatorMock = new Mock<INuGetValidator>();
            Validators.Add(name, validatorMock);

            return validatorMock;
        }
    }
}
