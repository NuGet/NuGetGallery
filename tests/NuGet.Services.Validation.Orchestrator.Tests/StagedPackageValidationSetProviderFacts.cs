// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class StagedPackageValidationSetProviderFacts
    {
        [Theory]
        [InlineData(StagedPackageStatus.Deleted, false)]
        [InlineData(StagedPackageStatus.Deleted, true)]
        [InlineData(StagedPackageStatus.Superseded, false)]
        [InlineData(StagedPackageStatus.Superseded, true)]
        public async Task RetiredAttemptsFinishExistingSetsWithoutReadingRetiredPrivateFiles(StagedPackageStatus status, bool hasExistingSet)
        {
            var storage = new Mock<IValidationStorageService>();
            var existing = hasExistingSet ? new PackageValidationSet { PackageKey = 43 } : null;
            storage.Setup(service => service.GetValidationSetAsync(It.IsAny<Guid>())).ReturnsAsync(existing);
            var files = new Mock<IValidationFileService>(MockBehavior.Strict);
            var blobs = new Mock<IStagingBlobService>(MockBehavior.Strict);
            var target = new TestableStagedPackageValidationSetProvider(files.Object, blobs.Object, storage.Object);
            var attempt = new StagedPackage
            {
                Key = 43,
                Status = status,
                StagedPackageIdentity = new StagedPackageIdentity { Package = new Package() },
            };

            var message = new ProcessValidationSetData("Test.Package", "1.0.0", Guid.NewGuid(), ValidatingType.StagedPackage, 43);
            var result = await target.TryGetOrCreateValidationSetAsync(message, new StagedPackageValidatingEntity(attempt));

            Assert.Same(existing, result);
            storage.Verify(service => service.OtherRecentValidationSetForPackageExists(
                It.IsAny<IValidatingEntity<StagedPackage>>(), It.IsAny<TimeSpan>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task CopiesUploadedBlobAndRecordsItsETag()
        {
            var packageUri = new Uri("https://example.test/staged-package");
            var stagedPackage = new StagedPackage
            {
                Key = 43,
                StagedPackageIdentityKey = 42,
                StagedPackageIdentity = new StagedPackageIdentity
                {
                    Key = 42,
                    Package = new Package { Key = 42 },
                    OwnerKey = 1,
                    Owner = new User("owner") { Key = 1 },
                },
                UploadedBlobPath = "package/path",
                UploadedBlobETag = "\"etag\"",
            };
            stagedPackage.StagedPackageIdentity.CurrentStagedPackageKey = stagedPackage.Key;
            stagedPackage.StagedPackageIdentity.CurrentStagedPackage = stagedPackage;
            var validationSet = new PackageValidationSet();
            var packageFileService = new Mock<IValidationFileService>();
            packageFileService
                .Setup(x => x.CopyPackageUrlForValidationSetAsync(validationSet, packageUri.AbsoluteUri))
                .Returns(Task.CompletedTask);
            var stagingBlobService = new Mock<IStagingBlobService>();
            stagingBlobService
                .Setup(x => x.GetPackageReadUriAsync(stagedPackage.UploadedBlobPath, stagedPackage.UploadedBlobETag))
                .ReturnsAsync(packageUri);
            var target = new TestableStagedPackageValidationSetProvider(
                packageFileService.Object,
                stagingBlobService.Object);

            await target.CopyPackageFileToValidationSetAsync(
                validationSet,
                new StagedPackageValidatingEntity(stagedPackage));

            Assert.Equal(stagedPackage.UploadedBlobETag, validationSet.PackageETag);
            packageFileService.Verify(
                x => x.CopyPackageUrlForValidationSetAsync(validationSet, packageUri.AbsoluteUri),
                Times.Once);
        }

        private class TestableStagedPackageValidationSetProvider : StagedPackageValidationSetProvider
        {
            public TestableStagedPackageValidationSetProvider(
                IValidationFileService packageFileService,
                IStagingBlobService stagingBlobService,
                IValidationStorageService storage = null)
                : base(
                    storage ?? Mock.Of<IValidationStorageService>(),
                    packageFileService,
                    stagingBlobService,
                    Mock.Of<IValidatorProvider>(),
                    Options(new ValidationConfiguration()),
                    Options(new SasDefinitionConfiguration()),
                    Mock.Of<ITelemetryService>(),
                    Mock.Of<ILogger<ValidationSetProvider<StagedPackage>>>())
            {
            }

            public new Task CopyPackageFileToValidationSetAsync(
                PackageValidationSet validationSet,
                IValidatingEntity<StagedPackage> validatingEntity)
            {
                return base.CopyPackageFileToValidationSetAsync(validationSet, validatingEntity);
            }

            private static IOptionsSnapshot<T> Options<T>(T value)
                where T : class
            {
                var options = new Mock<IOptionsSnapshot<T>>();
                options.SetupGet(x => x.Value).Returns(value);
                return options.Object;
            }
        }
    }
}
