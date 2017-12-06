// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class ValidationOutcomeProcessorFacts
    {
        [Fact]
        public async Task MarksPackageAsFailedOnFailedValidation()
        {
            AddValidation("validation1", ValidationStatus.Failed);

            PackageServiceMock
                .Setup(ps => ps.UpdatePackageStatusAsync(Package, PackageStatus.FailedValidation, true))
                .Returns(Task.FromResult(0))
                .Verifiable();

            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, Package);

            PackageServiceMock
                .Verify(ps => ps.UpdatePackageStatusAsync(Package, PackageStatus.FailedValidation, true), Times.Once());
            PackageServiceMock
                .Verify(ps => ps.UpdatePackageStatusAsync(It.IsAny<Package>(), It.IsAny<PackageStatus>(), It.IsAny<bool>()), Times.Once());
        }

        [Fact]
        public async Task ReEnqueuesProcessingIfNotAllComplete()
        {
            const int postponeMinutes = 1;
            AddValidation("validation1", ValidationStatus.Incomplete);
            Configuration.ValidationMessageRecheckPeriod = TimeSpan.FromMinutes(postponeMinutes);

            PackageValidationMessageData messageData = null;
            DateTimeOffset postponeTill = DateTimeOffset.MinValue;
            ValidationEnqueuerMock
                .Setup(ve => ve.StartValidationAsync(It.IsAny<PackageValidationMessageData>(), It.IsAny<DateTimeOffset>()))
                .Returns(Task.FromResult(0))
                .Callback<PackageValidationMessageData, DateTimeOffset>((pv, pt) => { messageData = pv; postponeTill = pt; });

            var processor = CreateProcessor();
            var startTime = DateTimeOffset.Now;
            await processor.ProcessValidationOutcomeAsync(ValidationSet, Package);

            ValidationEnqueuerMock
                .Verify(ve => ve.StartValidationAsync(It.IsAny<PackageValidationMessageData>(), It.IsAny<DateTimeOffset>()), Times.Once());
            Assert.NotNull(messageData);
            Assert.Equal(ValidationSet.ValidationTrackingId, messageData.ValidationTrackingId);
            Assert.Equal(ValidationSet.PackageId, messageData.PackageId);
            Assert.Equal(Package.Version, messageData.PackageVersion);
            Assert.Equal(postponeMinutes, (postponeTill - startTime).TotalMinutes, 0);
        }

        [Fact]
        public async Task CopiesPackageToPublicStorageUponSuccess()
        {
            AddValidation("validation1", ValidationStatus.Succeeded);
            Package.PackageStatusKey = PackageStatus.Validating;

            var stream = new MemoryStream();

            PackageFileServiceMock
                .Setup(pfs => pfs.DownloadValidationPackageFileAsync(Package))
                .ReturnsAsync(stream)
                .Verifiable();

            PackageFileServiceMock
                .Setup(pfs => pfs.SavePackageFileAsync(Package, stream))
                .Returns(Task.FromResult(0))
                .Verifiable();

            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, Package);

            PackageFileServiceMock
                .Verify(pfs => pfs.DownloadValidationPackageFileAsync(Package), Times.Once());
            PackageFileServiceMock
                .Verify(pfs => pfs.DownloadValidationPackageFileAsync(It.IsAny<Package>()), Times.Once());

            PackageFileServiceMock
                .Verify(pfs => pfs.SavePackageFileAsync(Package, stream), Times.Once());
            PackageFileServiceMock
                .Verify(pfs => pfs.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()), Times.Once());
        }

        [Fact]
        public async Task DoesNotCopyPackageIfItsAvailable()
        {
            AddValidation("validation1", ValidationStatus.Succeeded);
            Package.PackageStatusKey = PackageStatus.Available;

            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, Package);

            PackageFileServiceMock
                .Verify(pfs => pfs.DownloadValidationPackageFileAsync(It.IsAny<Package>()), Times.Never());

            PackageFileServiceMock
                .Verify(pfs => pfs.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()), Times.Never());
        }

        [Fact]
        public async Task MarksPackageAsAvailableOnSuccess()
        {
            AddValidation("validation1", ValidationStatus.Succeeded);
            Package.PackageStatusKey = PackageStatus.Validating;

            PackageServiceMock
                .Setup(ps => ps.UpdatePackageStatusAsync(Package, PackageStatus.Available, true))
                .Returns(Task.FromResult(0))
                .Verifiable();

            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, Package);

            PackageServiceMock
                .Verify(ps => ps.UpdatePackageStatusAsync(Package, PackageStatus.Available, true), Times.Once());
            PackageServiceMock
                .Verify(ps => ps.UpdatePackageStatusAsync(It.IsAny<Package>(), It.IsAny<PackageStatus>(), It.IsAny<bool>()), Times.Once());
        }

        [Fact]
        public async Task DeletesValidationPackageOnSuccess()
        {
            AddValidation("validation1", ValidationStatus.Succeeded);
            Package.PackageStatusKey = PackageStatus.Validating;

            PackageFileServiceMock
                .Setup(pfs => pfs.DeleteValidationPackageFileAsync(Package.PackageRegistration.Id, Package.Version))
                .Returns(Task.FromResult(0))
                .Verifiable();

            var procecssor = CreateProcessor();
            await procecssor.ProcessValidationOutcomeAsync(ValidationSet, Package);

            PackageFileServiceMock
                .Verify(pfs => pfs.DeleteValidationPackageFileAsync(Package.PackageRegistration.Id, Package.Version), Times.Once());
            PackageFileServiceMock
                .Verify(pfs => pfs.DeleteValidationPackageFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once());
        }

        [Fact]
        public async Task DeletesPackageFromPublicStorageOnDbUpdateFailure()
        {
            AddValidation("validation1", ValidationStatus.Succeeded);
            Package.PackageStatusKey = PackageStatus.Validating;

            const string exceptionText = "Everything failed";
            PackageServiceMock
                .Setup(ps => ps.UpdatePackageStatusAsync(Package, PackageStatus.Available, true))
                .Throws(new Exception(exceptionText))
                .Verifiable();

            PackageFileServiceMock
                .Setup(pfs => pfs.DeletePackageFileAsync(Package.PackageRegistration.Id, Package.Version))
                .Returns(Task.FromResult(0))
                .Verifiable();

            var procecssor = CreateProcessor();
            var exception = await Assert.ThrowsAsync<Exception>(() => procecssor.ProcessValidationOutcomeAsync(ValidationSet, Package));

            PackageFileServiceMock
                .Verify(pfs => pfs.DeletePackageFileAsync(Package.PackageRegistration.Id, Package.Version), Times.AtLeastOnce());
            Assert.Equal(exceptionText, exception.Message);
        }

        [Fact]
        public async Task CopyDbUpdateDeleteInCorrectOrder()
        {
            AddValidation("validation1", ValidationStatus.Succeeded);
            Package.PackageStatusKey = PackageStatus.Validating;

            var operations = new List<string>();

            var stream = new MemoryStream();
            PackageFileServiceMock
                .Setup(pfs => pfs.DownloadValidationPackageFileAsync(Package))
                .ReturnsAsync(stream)
                .Callback(() => operations.Add(nameof(ICorePackageFileService.DownloadValidationPackageFileAsync)));
            PackageFileServiceMock
                .Setup(pfs => pfs.SavePackageFileAsync(Package, stream))
                .Returns(Task.FromResult(0))
                .Callback(() => operations.Add(nameof(ICorePackageFileService.SavePackageFileAsync)));
            PackageServiceMock
                .Setup(ps => ps.UpdatePackageStatusAsync(Package, PackageStatus.Available, true))
                .Returns(Task.FromResult(0))
                .Callback(() => operations.Add(nameof(ICorePackageService.UpdatePackageStatusAsync)));
            PackageFileServiceMock
                .Setup(pfs => pfs.DeleteValidationPackageFileAsync(Package.PackageRegistration.Id, Package.Version))
                .Returns(Task.FromResult(0))
                .Callback(() => operations.Add(nameof(ICorePackageFileService.DeleteValidationPackageFileAsync)));

            var procecssor = CreateProcessor();
            await procecssor.ProcessValidationOutcomeAsync(ValidationSet, Package);

            var expectedOrder = new[]
            {
                nameof(ICorePackageFileService.DownloadValidationPackageFileAsync),
                nameof(ICorePackageFileService.SavePackageFileAsync),
                nameof(ICorePackageService.UpdatePackageStatusAsync),
                nameof(ICorePackageFileService.DeleteValidationPackageFileAsync)
            };

            Assert.Equal(expectedOrder, operations);
        }

        [Fact]
        public async Task DoesNotTakeDownAvailablePackages()
        {
            AddValidation("validation1", ValidationStatus.Failed);
            Package.PackageStatusKey = PackageStatus.Available;

            var procecssor = CreateProcessor();
            await procecssor.ProcessValidationOutcomeAsync(ValidationSet, Package);

            PackageServiceMock
                .Verify(ps => ps.UpdatePackageStatusAsync(Package, PackageStatus.FailedValidation, It.IsAny<bool>()), Times.Never());
        }

        [Theory]
        [InlineData(2, 1, 0, ValidationStatus.Incomplete, PackageStatus.Validating)]
        [InlineData(2, 1, 1, ValidationStatus.Incomplete, PackageStatus.Available)]
        [InlineData(3, 2, 0, ValidationStatus.Incomplete, PackageStatus.Validating)]
        [InlineData(3, 2, 1, ValidationStatus.Incomplete, PackageStatus.Validating)]
        [InlineData(3, 2, 2, ValidationStatus.Incomplete, PackageStatus.Available)]
        [InlineData(2, 1, 0, ValidationStatus.Failed, PackageStatus.FailedValidation)]
        [InlineData(3, 2, 0, ValidationStatus.Failed, PackageStatus.FailedValidation)]
        [InlineData(3, 2, 1, ValidationStatus.Failed, PackageStatus.FailedValidation)]
        public async Task PrefersDbOverConfigurationForDeterminingSuccess(
            int numConfiguredValidators,
            int numDbValidators,
            int numSucceededValidators,
            ValidationStatus notSucceededStatus,
            PackageStatus expectedStatus)
        {
            for (int cfgValidatorIndex = 0; cfgValidatorIndex < numConfiguredValidators; ++cfgValidatorIndex)
            {
                Configuration.Validations.Add(new ValidationConfigurationItem
                {
                    Name = "validation" + cfgValidatorIndex,
                    FailAfter = TimeSpan.FromDays(1),
                    RequiredValidations = new List<string> { }
                });
            }

            for (int dbValidatorIndex = 0; dbValidatorIndex < numDbValidators; ++dbValidatorIndex)
            {
                ValidationSet.PackageValidations.Add(new PackageValidation
                {
                    Type = "validation" + dbValidatorIndex,
                    ValidationStatus = dbValidatorIndex < numSucceededValidators ? ValidationStatus.Succeeded : notSucceededStatus
                });
            }

            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, Package);

            if (expectedStatus != PackageStatus.Validating)
            {
                PackageServiceMock
                    .Verify(ps => ps.UpdatePackageStatusAsync(Package, expectedStatus, true), Times.Once());
                PackageServiceMock
                    .Verify(ps => ps.UpdatePackageStatusAsync(It.IsAny<Package>(), It.IsAny<PackageStatus>(), It.IsAny<bool>()), Times.Once());
            }
            else
            {
                PackageServiceMock
                    .Verify(ps => ps.UpdatePackageStatusAsync(It.IsAny<Package>(), It.IsAny<PackageStatus>(), It.IsAny<bool>()), Times.Never());
            }
        }

        public ValidationOutcomeProcessorFacts()
        {
            PackageServiceMock = new Mock<ICorePackageService>();
            PackageFileServiceMock = new Mock<ICorePackageFileService>();
            ValidationEnqueuerMock = new Mock<IPackageValidationEnqueuer>();
            ConfigurationAccessorMock = new Mock<IOptionsSnapshot<ValidationConfiguration>>();
            LoggerMock = new Mock<ILogger<ValidationOutcomeProcessor>>();

            Configuration = new ValidationConfiguration();
            Configuration.Validations = new List<ValidationConfigurationItem>();
            Package = new Package
            {
                PackageRegistration = new PackageRegistration { Id = "package" },
                Version = "1.2.3.456",
                NormalizedVersion = "1.2.3",
                PackageStatusKey = PackageStatus.Validating
            };
            Package.PackageRegistration.Packages.Add(Package);

            ValidationSet = new PackageValidationSet();
            ValidationSet.PackageValidations = new List<PackageValidation>();

            ValidationSet.PackageId = Package.PackageRegistration.Id;
            ValidationSet.PackageNormalizedVersion = Package.NormalizedVersion;
            ValidationSet.ValidationTrackingId = Guid.NewGuid();

            ConfigurationAccessorMock
                .SetupGet(ca => ca.Value)
                .Returns(Configuration);
        }

        protected ValidationOutcomeProcessor CreateProcessor()
        {
            return new ValidationOutcomeProcessor(
                PackageServiceMock.Object,
                PackageFileServiceMock.Object,
                ValidationEnqueuerMock.Object,
                ConfigurationAccessorMock.Object,
                LoggerMock.Object
                );
        }

        protected Mock<ICorePackageService> PackageServiceMock { get; }
        protected Mock<ICorePackageFileService> PackageFileServiceMock { get; }
        protected Mock<IPackageValidationEnqueuer> ValidationEnqueuerMock { get; }
        protected Mock<IOptionsSnapshot<ValidationConfiguration>> ConfigurationAccessorMock { get; }
        protected Mock<ILogger<ValidationOutcomeProcessor>> LoggerMock { get; }
        protected ValidationConfiguration Configuration { get; }
        protected PackageValidationSet ValidationSet { get; }
        protected Package Package { get; }

        private void AddValidation(string validationName, ValidationStatus validationStatus)
        {
            ValidationSet.PackageValidations.Add(new PackageValidation
            {
                Type = validationName,
                ValidationStatus = validationStatus
            });
            Configuration.Validations.Add(new ValidationConfigurationItem
            {
                Name = validationName,
                FailAfter = TimeSpan.FromDays(1),
                RequiredValidations = new List<string> { }
            });
        }
    }
}
