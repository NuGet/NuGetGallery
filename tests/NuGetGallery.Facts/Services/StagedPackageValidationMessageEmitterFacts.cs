// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using Xunit;

namespace NuGetGallery
{
    public class StagedPackageValidationMessageEmitterFacts
    {
        [Fact]
        public async Task EnqueuesStagedPackageValidation()
        {
            PackageValidationMessageData message = null;
            var enqueuer = new Mock<IPackageValidationEnqueuer>();
            enqueuer
                .Setup(x => x.SendMessageAsync(It.IsAny<PackageValidationMessageData>(), It.IsAny<DateTimeOffset>()))
                .Callback<PackageValidationMessageData, DateTimeOffset>((value, _) => message = value)
                .Returns(Task.CompletedTask);
            var target = new StagedPackageValidationMessageEmitter(
                enqueuer.Object,
                Mock.Of<IAppConfiguration>(),
                Mock.Of<IDiagnosticsService>());
            var stagedPackage = new StagedPackage
            {
                Key = 43,
                StagedPackageIdentityKey = 42,
                StagedPackageIdentity = new StagedPackageIdentity
                {
                    Key = 42,
                    Package = new Package
                    {
                        Key = 42,
                        PackageRegistration = new PackageRegistration { Id = "PackageA" },
                        Version = "1.0.0",
                    },
                    OwnerKey = 1,
                    Owner = new User("owner") { Key = 1 },
                },
            };
            stagedPackage.StagedPackageIdentity.CurrentStagedPackageKey = stagedPackage.Key;
            stagedPackage.StagedPackageIdentity.CurrentStagedPackage = stagedPackage;

            var status = await target.StartValidationAsync(stagedPackage);

            Assert.Equal(StagedPackageStatus.Validating, status);
            Assert.Equal("PackageA", message.ProcessValidationSet.PackageId);
            Assert.Equal("1.0.0", message.ProcessValidationSet.PackageVersion);
            Assert.Equal(ValidatingType.StagedPackage, message.ProcessValidationSet.ValidatingType);
            Assert.Equal(stagedPackage.Key, message.ProcessValidationSet.EntityKey);
        }

        [Fact]
        public async Task ImmediateEmitterPreservesTheUploadAndPreparesValidatedContentBeforeReady()
        {
            var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            var storage = new Mock<IStagingBlobService>(MockBehavior.Strict);
            storage.Setup(service => service.OpenPackageFileAsync("uploaded/package.nupkg", "upload-etag")).ReturnsAsync(stream);
            storage.Setup(service => service.SaveValidatedPackageFileAsync("PackageA", "1.0.0", stream))
                .ReturnsAsync(new StagingFileReference("validated/package.nupkg", "validated-etag"));
            var target = new ImmediateStagedPackageValidationMessageEmitter(storage.Object);
            var stagedPackage = new StagedPackage
            {
                UploadedBlobPath = "uploaded/package.nupkg",
                UploadedBlobETag = "upload-etag",
                StagedPackageIdentity = new StagedPackageIdentity
                {
                    Package = new Package { PackageRegistration = new PackageRegistration { Id = "PackageA" }, NormalizedVersion = "1.0.0" },
                },
            };

            var status = await target.StartValidationAsync(stagedPackage);

            Assert.Equal(StagedPackageStatus.Ready, status);
            Assert.Equal("uploaded/package.nupkg", stagedPackage.UploadedBlobPath);
            Assert.Equal("upload-etag", stagedPackage.UploadedBlobETag);
            Assert.Equal("validated/package.nupkg", stagedPackage.ValidatedBlobPath);
            Assert.Equal("validated-etag", stagedPackage.ValidatedBlobETag);
            Assert.False(stream.CanRead);
            storage.Verify(service => service.SaveValidatedPackageFileAsync("PackageA", "1.0.0", stream), Times.Once);
        }

        [Fact]
        public async Task ImmediateEmitterDoesNotRecordValidatedContentWhenPreparationFails()
        {
            var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            var storage = new Mock<IStagingBlobService>();
            storage.Setup(service => service.OpenPackageFileAsync("uploaded/package.nupkg", "upload-etag")).ReturnsAsync(stream);
            var expected = new InvalidOperationException("Hash metadata could not be saved.");
            storage.Setup(service => service.SaveValidatedPackageFileAsync("PackageA", "1.0.0", stream)).ThrowsAsync(expected);
            var stagedPackage = new StagedPackage
            {
                Status = StagedPackageStatus.Validating,
                UploadedBlobPath = "uploaded/package.nupkg",
                UploadedBlobETag = "upload-etag",
                StagedPackageIdentity = new StagedPackageIdentity
                {
                    Package = new Package { PackageRegistration = new PackageRegistration { Id = "PackageA" }, NormalizedVersion = "1.0.0" },
                },
            };
            var target = new ImmediateStagedPackageValidationMessageEmitter(storage.Object);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => target.StartValidationAsync(stagedPackage));

            Assert.Same(expected, actual);
            Assert.Null(stagedPackage.ValidatedBlobPath);
            Assert.Null(stagedPackage.ValidatedBlobETag);
            Assert.Equal(StagedPackageStatus.Validating, stagedPackage.Status);
            Assert.Equal("upload-etag", stagedPackage.UploadedBlobETag);
            Assert.False(stream.CanRead);
        }
    }
}
