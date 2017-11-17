// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class StrictStorageFacts : ValidationSetProcessorFactsBase
    {
        public StrictStorageFacts()
            : base(validationStorageMockBehavior: MockBehavior.Strict)
        {

        }

        [Fact]
        public async Task FailsUnknownValidations()
        {
            AddValidationToSet("validation1");
            ValidationStorageMock
                .Setup(vs => vs.UpdateValidationStatusAsync(ValidationSet.PackageValidations.First(), ValidationStatus.Failed))
                .Returns(Task.FromResult(0))
                .Verifiable();

            var processor = CreateProcessor();
            await processor.ProcessValidationsAsync(ValidationSet, Package);

            ValidationStorageMock
                .Verify(vs => vs.UpdateValidationStatusAsync(ValidationSet.PackageValidations.First(), ValidationStatus.Failed), Times.Once());
        }
    }

    public class DefaultMockFacts : ValidationSetProcessorFactsBase
    {
        [Theory]
        [InlineData(ValidationStatus.NotStarted, false)]
        [InlineData(ValidationStatus.Incomplete, true)]
        [InlineData(ValidationStatus.Succeeded, true)]
        [InlineData(ValidationStatus.Failed, true)]
        public async Task StartsNotStartedValidations(ValidationStatus startStatus, bool expectStorageUdpate)
        {
            UseDefaultValidatorProvider();
            const string validationName = "validation1";
            var validator = AddValidation(validationName, TimeSpan.FromDays(1));
            var validation = ValidationSet.PackageValidations.First();

            ValidationStatus actualStatus = ValidationStatus.NotStarted;
            validator
                .Setup(v => v.GetStatusAsync(It.IsAny<IValidationRequest>()))
                .Returns(() => Task.FromResult(actualStatus))
                .Callback<IValidationRequest>(r => Assert.Equal(validation.Key, r.ValidationId));

            validator
                .Setup(v => v.StartValidationAsync(It.IsAny<IValidationRequest>()))
                .ReturnsAsync(startStatus)
                .Callback<IValidationRequest>(r => {
                    Assert.Equal(validation.Key, r.ValidationId);
                    actualStatus = startStatus;
                })
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.MarkValidationStartedAsync(validation, startStatus))
                .Returns(Task.FromResult(0))
                .Callback<PackageValidation, ValidationStatus>((pv, vs) => pv.ValidationStatus = vs)
                .Verifiable();

            var processor = CreateProcessor();
            await processor.ProcessValidationsAsync(ValidationSet, Package);

            validator.Verify(v => v.StartValidationAsync(It.IsAny<IValidationRequest>()), Times.AtLeastOnce());
            if (expectStorageUdpate)
            {
                ValidationStorageMock.Verify(
                    vs => vs.MarkValidationStartedAsync(validation, startStatus), Times.Once());
                ValidationStorageMock.Verify(
                    vs => vs.MarkValidationStartedAsync(It.IsAny<PackageValidation>(), It.IsAny<ValidationStatus>()), Times.Once());
            }
            else
            {
                ValidationStorageMock.Verify(
                    vs => vs.MarkValidationStartedAsync(It.IsAny<PackageValidation>(), It.IsAny<ValidationStatus>()), Times.Never());
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
                .Setup(v => v.GetStatusAsync(It.IsAny<IValidationRequest>()))
                .ReturnsAsync(ValidationStatus.Incomplete);

            validator2
                .Setup(v => v.GetStatusAsync(It.IsAny<IValidationRequest>()))
                .ReturnsAsync(ValidationStatus.NotStarted);

            validator2
                .Setup(v => v.StartValidationAsync(It.IsAny<IValidationRequest>()))
                .ReturnsAsync(ValidationStatus.Incomplete)
                .Verifiable();

            var processor = CreateProcessor();
            await processor.ProcessValidationsAsync(ValidationSet, Package);

            validator2
                .Verify(v => v.StartValidationAsync(It.IsAny<IValidationRequest>()), Times.Never());
        }

        [Theory]
        [InlineData(ValidationStatus.Incomplete, false)]
        [InlineData(ValidationStatus.Succeeded, true)]
        [InlineData(ValidationStatus.Failed, true)]
        public async Task HandlesIncompleteValidationsStatusChanges(ValidationStatus targetStatus, bool expectStorageUdpate)
        {
            UseDefaultValidatorProvider();
            var validator = AddValidation("validation1", TimeSpan.FromDays(1), validationStatus: ValidationStatus.Incomplete);
            var validation = ValidationSet.PackageValidations.First();

            validator
                .Setup(v => v.GetStatusAsync(It.IsAny<IValidationRequest>()))
                .ReturnsAsync(targetStatus)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.UpdateValidationStatusAsync(validation, targetStatus))
                .Returns(Task.FromResult(0))
                .Callback<PackageValidation, ValidationStatus>((pv, vs) => pv.ValidationStatus = vs)
                .Verifiable();

            var processor = CreateProcessor();
            await processor.ProcessValidationsAsync(ValidationSet, Package);

            validator
                .Verify(v => v.GetStatusAsync(It.IsAny<IValidationRequest>()), Times.AtLeastOnce());
            if (expectStorageUdpate)
            {
                ValidationStorageMock.Verify(
                    vs => vs.UpdateValidationStatusAsync(validation, targetStatus), Times.Once());
                ValidationStorageMock.Verify(
                    vs => vs.UpdateValidationStatusAsync(It.IsAny<PackageValidation>(), It.IsAny<ValidationStatus>()), Times.Once());
            }
            else
            {
                ValidationStorageMock.Verify(
                    vs => vs.UpdateValidationStatusAsync(It.IsAny<PackageValidation>(), It.IsAny<ValidationStatus>()), Times.Never());
            }
            Assert.Equal(targetStatus, validation.ValidationStatus);
        }

        [Theory]
        [InlineData(true, false, PublicContainerName)]
        [InlineData(false, true, ValidationContainerName)]
        public async Task UsesProperNupkgUrl(bool existsInPublicContainer, bool existsInValidationContainer, string expectedUrlSubstring)
        {
            UseDefaultValidatorProvider();
            var validator = AddValidation("validation1", TimeSpan.FromDays(1));

            PackageFileServiceMock
                .Setup(pfs => pfs.DoesPackageFileExistAsync(Package))
                .ReturnsAsync(existsInPublicContainer);
            PackageFileServiceMock
                .Setup(pfs => pfs.DoesValidationPackageFileExistAsync(Package))
                .ReturnsAsync(existsInValidationContainer);
            IValidationRequest validationRequest = null;
            validator
                .Setup(v => v.GetStatusAsync(It.IsAny<IValidationRequest>()))
                .ReturnsAsync(ValidationStatus.NotStarted)
                .Callback<IValidationRequest>(vr => validationRequest = vr);

            var processor = CreateProcessor();
            await processor.ProcessValidationsAsync(ValidationSet, Package);

            validator
                .Verify(v => v.GetStatusAsync(It.IsAny<IValidationRequest>()), Times.AtLeastOnce());
            Assert.NotNull(validationRequest);
            Assert.Contains(expectedUrlSubstring, validationRequest.NupkgUrl);
            Assert.Equal(Package.PackageRegistration.Id, validationRequest.PackageId);
            Assert.Equal(Package.NormalizedVersion, validationRequest.PackageVersion);
        }

        [Fact]
        public void ThrowsIfNupkgDoesNotExist()
        {
            UseDefaultValidatorProvider();
            var validator = AddValidation("validation1", TimeSpan.FromDays(1));

            PackageFileServiceMock
                .Setup(pfs => pfs.DoesPackageFileExistAsync(Package))
                .ReturnsAsync(false);
            PackageFileServiceMock
                .Setup(pfs => pfs.DoesValidationPackageFileExistAsync(Package))
                .ReturnsAsync(false);

            var processor = CreateProcessor();
            var ex = Assert.ThrowsAsync<Exception>(() => processor.ProcessValidationsAsync(ValidationSet, Package));

            PackageFileServiceMock
                .Verify(pfs => pfs.DoesPackageFileExistAsync(Package), Times.Once());
            PackageFileServiceMock
                .Verify(pfs => pfs.DoesPackageFileExistAsync(It.IsAny<Package>()), Times.Once());
            PackageFileServiceMock
                .Verify(pfs => pfs.DoesValidationPackageFileExistAsync(Package), Times.Once());
            PackageFileServiceMock
                .Verify(pfs => pfs.DoesValidationPackageFileExistAsync(It.IsAny<Package>()), Times.Once());
        }
    }

    public abstract class ValidationSetProcessorFactsBase
    {
        protected const string PublicContainerName = "packages-container";
        protected const string ValidationContainerName = "validation-container";

        protected Mock<IValidatorProvider> ValidatorProviderMock { get; }
        protected Mock<IValidationStorageService> ValidationStorageMock { get; }
        protected Mock<IOptionsSnapshot<ValidationConfiguration>> ConfigurationAccessorMock { get; }
        protected Mock<ICorePackageFileService> PackageFileServiceMock { get; }
        protected Mock<ILogger<ValidationSetProcessor>> LoggerMock { get; }
        protected ValidationConfiguration Configuration { get; }
        protected Package Package { get; }
        protected PackageValidationSet ValidationSet { get; }
        protected Dictionary<string, Mock<IValidator>> Validators { get; }

        protected ValidationSetProcessorFactsBase(
            MockBehavior validatorProviderMockBehavior = MockBehavior.Default,
            MockBehavior validationStorageMockBehavior = MockBehavior.Default,
            MockBehavior configurationAccessorMockBehavior = MockBehavior.Default,
            MockBehavior packageFileServiceMockBehavior = MockBehavior.Default,
            MockBehavior loggerMockBehavior = MockBehavior.Default)
        {
            ValidatorProviderMock = new Mock<IValidatorProvider>(validatorProviderMockBehavior);
            ValidationStorageMock = new Mock<IValidationStorageService>(validationStorageMockBehavior);
            ConfigurationAccessorMock = new Mock<IOptionsSnapshot<ValidationConfiguration>>(configurationAccessorMockBehavior);
            PackageFileServiceMock = new Mock<ICorePackageFileService>(packageFileServiceMockBehavior);
            LoggerMock = new Mock<ILogger<ValidationSetProcessor>>(loggerMockBehavior);
            Configuration = new ValidationConfiguration
            {
                Validations = new List<ValidationConfigurationItem>
                {
                }
            };
            ConfigurationAccessorMock
                .SetupGet(ca => ca.Value)
                .Returns(Configuration);

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
                PackageId = Package.PackageRegistration.Id,
                PackageNormalizedVersion = Package.NormalizedVersion,
                PackageValidations = new List<PackageValidation>
                {
                }
            };
            ValidationSet.PackageValidations.ToList().ForEach(v => v.PackageValidationSet = ValidationSet);
            Validators = new Dictionary<string, Mock<IValidator>>();

            PackageFileServiceMock
                .Setup(pfs => pfs.GetValidationPackageReadUriAsync(It.IsAny<Package>(), It.IsAny<DateTimeOffset>()))
                .Returns<Package, DateTimeOffset>(
                    (p, e) => Task.FromResult(new Uri($"https://example.com/{ValidationContainerName}/{p.PackageRegistration.Id}/{p.NormalizedVersion}?e={e:yyyy-MM-dd-hh-mm-ss}")));

            PackageFileServiceMock
                .Setup(pfs => pfs.GetPackageReadUriAsync(It.IsAny<Package>()))
                .Returns<Package>(
                    p => Task.FromResult(new Uri($"https://example.com/{PublicContainerName}/{p.PackageRegistration.Id}/{p.NormalizedVersion}")));

            PackageFileServiceMock
                .Setup(pfs => pfs.DoesValidationPackageFileExistAsync(Package))
                .ReturnsAsync(true);
        }

        protected ValidationSetProcessor CreateProcessor()
            => new ValidationSetProcessor(
                ValidatorProviderMock.Object,
                ValidationStorageMock.Object,
                ConfigurationAccessorMock.Object,
                PackageFileServiceMock.Object,
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

        protected ValidationConfigurationItem AddValidationToConfiguration(string name, TimeSpan failAfter, params string[] requiredValidations)
        {
            var validation = new ValidationConfigurationItem
            {
                Name = name,
                FailAfter = failAfter,
                RequiredValidations = requiredValidations.ToList()
            };
            Configuration.Validations.Add(validation);
            return validation;
        }

        protected void UseDefaultValidatorProvider()
        {
            ValidatorProviderMock
                .Setup(vp => vp.GetValidator(It.IsAny<string>()))
                .Returns<string>(name => Validators[name].Object);
        }

        protected Mock<IValidator> AddValidation(
            string name,
            TimeSpan failAfter,
            string[] requiredValidations = null,
            ValidationStatus validationStatus = ValidationStatus.NotStarted)
        {
            requiredValidations = requiredValidations ?? new string[0];
            AddValidationToSet(name, validationStatus);
            AddValidationToConfiguration(name, failAfter, requiredValidations);

            var validatorMock = new Mock<IValidator>();
            Validators.Add(name, validatorMock);

            return validatorMock;
        }
    }
}
