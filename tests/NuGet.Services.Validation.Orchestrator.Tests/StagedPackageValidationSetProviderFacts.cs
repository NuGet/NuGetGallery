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
        [Fact]
        public async Task CopiesUploadedBlobAndRecordsItsETag()
        {
            var packageUri = new Uri("https://example.test/staged-package");
            var stagedPackage = new StagedPackage
            {
                Key = 43,
                UploadedBlobPath = "package/path",
                UploadedBlobETag = "\"etag\"",
            };
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
                IStagingBlobService stagingBlobService)
                : base(
                    Mock.Of<IValidationStorageService>(),
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
