// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery;
using NuGetGallery.Packaging;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class StagedPackageStatusProcessorFacts
    {
        [Fact]
        public async Task SuccessfulValidationPreservesUploadedBlobAndStoresValidatedBlob()
        {
            const string uploadedPath = "uploaded";
            const string uploadedETag = "uploaded-etag";
            const string validatedPath = "validated";
            const string validatedETag = "validated-etag";

            var stagedPackage = new StagedPackage
            {
                Key = 43,
                PackageKey = 42,
                UploadedBlobPath = uploadedPath,
                UploadedBlobETag = uploadedETag,
                Status = StagedPackageStatus.Validating,
            };
            var validatingEntity = new StagedPackageValidatingEntity(stagedPackage);
            var validationSet = new PackageValidationSet
            {
                PackageKey = stagedPackage.Key,
                PackageId = "PackageA",
                PackageNormalizedVersion = "1.0.0",
                PackageETag = uploadedETag,
            };
            var metadata = new PackageStreamMetadata();
            var validatedFileUri = new Uri("https://example.test/validated");
            var validatedFile = new StagingFileReference(validatedPath, validatedETag);

            var entityService = new Mock<IEntityService<StagedPackage>>();
            entityService
                .Setup(x => x.UpdateMetadataAsync(stagedPackage, metadata, false))
                .Returns(Task.CompletedTask);
            entityService
                .Setup(x => x.UpdateStatusAsync(stagedPackage, PackageStatus.Available, true))
                .Returns(Task.CompletedTask);

            var packageFileService = new Mock<IValidationFileService>();
            packageFileService
                .Setup(x => x.UpdatePackageBlobMetadataInValidationSetAsync(validationSet))
                .ReturnsAsync(metadata);
            packageFileService
                .Setup(x => x.GetPackageForValidationSetReadUriAsync(
                    validationSet,
                    null,
                    It.IsAny<DateTimeOffset>()))
                .ReturnsAsync(validatedFileUri);

            var stagingBlobService = new Mock<IStagingBlobService>();
            stagingBlobService
                .Setup(x => x.CopyPackageFileToStagingAsync(
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion,
                    validatedFileUri))
                .ReturnsAsync(validatedFile);

            var target = new StagedPackageStatusProcessor(
                entityService.Object,
                packageFileService.Object,
                stagingBlobService.Object);

            await target.SetStatusAsync(validatingEntity, validationSet, PackageStatus.Available);

            Assert.Equal(uploadedPath, stagedPackage.UploadedBlobPath);
            Assert.Equal(uploadedETag, stagedPackage.UploadedBlobETag);
            Assert.Equal(validatedPath, stagedPackage.ValidatedBlobPath);
            Assert.Equal(validatedETag, stagedPackage.ValidatedBlobETag);
            entityService.Verify(x => x.UpdateMetadataAsync(stagedPackage, metadata, false), Times.Once);
            entityService.Verify(x => x.UpdateStatusAsync(stagedPackage, PackageStatus.Available, true), Times.Once);
        }

        [Fact]
        public async Task FailedValidationUpdatesOnlyTheStagedStatus()
        {
            var stagedPackage = CreateStagedPackage();
            var validationSet = CreateValidationSet();
            var entityService = new Mock<IEntityService<StagedPackage>>();
            entityService
                .Setup(x => x.UpdateStatusAsync(stagedPackage, PackageStatus.FailedValidation, true))
                .Returns(Task.CompletedTask);
            var packageFileService = new Mock<IValidationFileService>(MockBehavior.Strict);
            var stagingBlobService = new Mock<IStagingBlobService>(MockBehavior.Strict);
            var target = new StagedPackageStatusProcessor(
                entityService.Object,
                packageFileService.Object,
                stagingBlobService.Object);

            await target.SetStatusAsync(
                new StagedPackageValidatingEntity(stagedPackage),
                validationSet,
                PackageStatus.FailedValidation);

            entityService.Verify(
                x => x.UpdateStatusAsync(stagedPackage, PackageStatus.FailedValidation, true),
                Times.Once);
        }

        [Theory]
        [InlineData(44, "uploaded-etag", StagedPackageStatus.Validating)]
        [InlineData(43, "different-etag", StagedPackageStatus.Validating)]
        [InlineData(43, "uploaded-etag", StagedPackageStatus.Ready)]
        public async Task IgnoresOutcomeForNonCurrentAttempt(
            int validationSetPackageKey,
            string validationSetPackageETag,
            StagedPackageStatus stagedPackageStatus)
        {
            var stagedPackage = CreateStagedPackage();
            stagedPackage.Status = stagedPackageStatus;
            var validationSet = CreateValidationSet();
            validationSet.PackageKey = validationSetPackageKey;
            validationSet.PackageETag = validationSetPackageETag;
            var entityService = new Mock<IEntityService<StagedPackage>>(MockBehavior.Strict);
            var packageFileService = new Mock<IValidationFileService>(MockBehavior.Strict);
            var stagingBlobService = new Mock<IStagingBlobService>(MockBehavior.Strict);
            var target = new StagedPackageStatusProcessor(
                entityService.Object,
                packageFileService.Object,
                stagingBlobService.Object);

            await target.SetStatusAsync(
                new StagedPackageValidatingEntity(stagedPackage),
                validationSet,
                PackageStatus.Available);
        }

        private static StagedPackage CreateStagedPackage()
        {
            return new StagedPackage
            {
                Key = 43,
                PackageKey = 42,
                UploadedBlobPath = "uploaded",
                UploadedBlobETag = "uploaded-etag",
                Status = StagedPackageStatus.Validating,
            };
        }

        private static PackageValidationSet CreateValidationSet()
        {
            return new PackageValidationSet
            {
                PackageKey = 43,
                PackageId = "PackageA",
                PackageNormalizedVersion = "1.0.0",
                PackageETag = "uploaded-etag",
            };
        }
    }
}
