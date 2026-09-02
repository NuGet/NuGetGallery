// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Staging.Promotion.Tests
{
    public class StagedPackagePromotionMessageHandlerFacts
    {
        [Fact]
        public async Task PublishesExactValidatedPackageAndRemovesStagingRow()
        {
            var context = new TestContext();

            var handled = await context.Target.HandleAsync(context.Message);

            Assert.True(handled);
            Assert.Equal(PackageStatus.Available, context.Package.PackageStatusKey);
            Assert.Equal(CoreConstants.Sha512HashAlgorithmId, context.Package.HashAlgorithm);
            Assert.Equal(context.Content.Length, context.Package.PackageFileSize);
            using (var sha512 = SHA512.Create())
            {
                Assert.Equal(
                    Convert.ToBase64String(sha512.ComputeHash(context.Content)),
                    context.Package.Hash);
            }
            context.StagingBlobService.Verify(
                x => x.OpenPackageFileAsync(
                    context.StagedPackage.ValidatedBlobPath,
                    context.StagedPackage.ValidatedBlobETag),
                Times.Once);
            context.PackageFileStorageService.Verify(
                x => x.CopyFileAsync(
                    context.SourceUri,
                    CoreConstants.Folders.PackagesFolderName,
                    "example.package.1.2.3.nupkg",
                    It.Is<IAccessCondition>(condition => condition.IfNoneMatchETag == "*")),
                Times.Once);
            Assert.Equal(CoreConstants.DefaultCacheControl, context.PublicBlobProperties.Object.CacheControl);
            context.PackageService.Verify(
                x => x.UpdatePackageStreamMetadataAsync(
                    context.Package,
                    It.IsAny<NuGetGallery.Packaging.PackageStreamMetadata>(),
                    false),
                Times.Once);
            context.PackageService.Verify(
                x => x.UpdatePackageStatusAsync(context.Package, PackageStatus.Available, false),
                Times.Once);
            context.StagedPackageRepository.Verify(
                x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()),
                Times.Once);
            context.StagedPackageRepository.Verify(x => x.DeleteOnCommit(context.StagedPackage), Times.Once);
            context.StagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
            Assert.Empty(context.StagedPackages);
        }

        [Fact]
        public async Task ConsumesDuplicateDeliveryAfterPromotionCompletes()
        {
            var context = new TestContext();

            var firstHandled = await context.Target.HandleAsync(context.Message);
            var duplicateHandled = await context.Target.HandleAsync(context.Message);

            Assert.True(firstHandled);
            Assert.True(duplicateHandled);
            context.PackageFileStorageService.Verify(
                x => x.CopyFileAsync(
                    context.SourceUri,
                    CoreConstants.Folders.PackagesFolderName,
                    "example.package.1.2.3.nupkg",
                    It.IsAny<IAccessCondition>()),
                Times.Once);
            context.StagedPackageRepository.Verify(
                x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()),
                Times.Once);
        }

        [Fact]
        public async Task ConsumesOrphanMessage()
        {
            var context = new TestContext();
            context.StagedPackages.Clear();

            var handled = await context.Target.HandleAsync(context.Message);

            Assert.True(handled);
            context.VerifyNotPublished();
        }

        [Fact]
        public async Task ConsumesMessageForEarlierPromotionAttempt()
        {
            var context = new TestContext();
            context.StagedPackage.ActivePromotionId = Guid.NewGuid();

            var handled = await context.Target.HandleAsync(context.Message);

            Assert.True(handled);
            context.VerifyNotPublished();
        }

        [Fact]
        public async Task ConsumesMessageWhenPromotionIsNoLongerActive()
        {
            var context = new TestContext();
            context.StagedPackage.Status = StagedPackageStatus.Ready;

            var handled = await context.Target.HandleAsync(context.Message);

            Assert.True(handled);
            context.VerifyNotPublished();
        }

        [Fact]
        public async Task ConsumesMessageWhenPackageIsNoLongerStaged()
        {
            var context = new TestContext();
            context.Package.PackageStatusKey = PackageStatus.Available;

            var handled = await context.Target.HandleAsync(context.Message);

            Assert.True(handled);
            context.VerifyNotPublished();
        }

        [Theory]
        [InlineData(InvalidState.MissingValidatedBlob)]
        [InlineData(InvalidState.OwnerNoLongerOwnsPackage)]
        public async Task MarksPromotionFailedWhenRequiredStateIsMissing(InvalidState state)
        {
            var context = new TestContext();
            context.Apply(state);

            var handled = await context.Target.HandleAsync(context.Message);

            Assert.True(handled);
            Assert.Equal(StagedPackageStatus.PromotionFailed, context.StagedPackage.Status);
            context.VerifyNotPublished();
            context.StagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ExtractsEmbeddedLicenseAndReadmeFromValidatedPackage()
        {
            var context = new TestContext();
            context.Package.EmbeddedLicenseType = EmbeddedLicenseFileType.PlainText;
            context.Package.EmbeddedReadmeType = EmbeddedReadmeFileType.Markdown;
            context.Package.HasReadMe = true;

            await context.Target.HandleAsync(context.Message);

            context.LicenseFileService.Verify(
                x => x.ExtractAndSaveLicenseFileAsync(context.Package, It.IsAny<Stream>()),
                Times.Once);
            context.ReadmeFileService.Verify(
                x => x.ExtractAndSaveReadmeFileAsync(context.Package, It.IsAny<Stream>()),
                Times.Once);
            context.StagingBlobService.Verify(
                x => x.OpenPackageFileAsync(
                    context.StagedPackage.ValidatedBlobPath,
                    context.StagedPackage.ValidatedBlobETag),
                Times.Exactly(2));
        }

        [Fact]
        public async Task RemovesPublishedPackageWhenBlobPropertyUpdateFails()
        {
            var context = new TestContext();
            var expected = new InvalidOperationException("Property update failed.");
            context.PackageFileStorageService
                .Setup(x => x.SetPropertiesAsync(
                    CoreConstants.Folders.PackagesFolderName,
                    "example.package.1.2.3.nupkg",
                    It.IsAny<Func<Lazy<Task<Stream>>, ICloudBlobProperties, Task<bool>>>()))
                .ThrowsAsync(expected);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.Target.HandleAsync(context.Message));

            Assert.Same(expected, actual);
            Assert.Equal(StagedPackageStatus.Promoting, context.StagedPackage.Status);
            context.PackageFileStorageService.Verify(
                x => x.DeleteFileAsync(
                    CoreConstants.Folders.PackagesFolderName,
                    "example.package.1.2.3.nupkg"),
                Times.Once);
            context.StagedPackageRepository.Verify(
                x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()),
                Times.Never);
        }

        [Fact]
        public async Task RemovesPublishedFilesWhenContentExtractionFails()
        {
            var context = new TestContext();
            var expected = new InvalidOperationException("Readme extraction failed.");
            context.Package.EmbeddedLicenseType = EmbeddedLicenseFileType.PlainText;
            context.Package.EmbeddedReadmeType = EmbeddedReadmeFileType.Markdown;
            context.Package.HasReadMe = true;
            context.ReadmeFileService
                .Setup(x => x.ExtractAndSaveReadmeFileAsync(context.Package, It.IsAny<Stream>()))
                .ThrowsAsync(expected);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.Target.HandleAsync(context.Message));

            Assert.Same(expected, actual);
            Assert.Equal(StagedPackageStatus.Promoting, context.StagedPackage.Status);
            context.PackageFileStorageService.Verify(
                x => x.DeleteFileAsync(
                    CoreConstants.Folders.PackagesFolderName,
                    "example.package.1.2.3.nupkg"),
                Times.Once);
            context.LicenseFileService.Verify(
                x => x.DeleteLicenseFileAsync(context.Package.Id, context.Package.NormalizedVersion),
                Times.Once);
            context.ReadmeFileService.Verify(
                x => x.DeleteReadmeFileAsync(context.Package.Id, context.Package.NormalizedVersion),
                Times.Once);
            context.StagedPackageRepository.Verify(
                x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()),
                Times.Never);
        }

        [Fact]
        public async Task RemovesPublishedPackageWhenDatabaseCommitFails()
        {
            var context = new TestContext();
            var expected = new InvalidOperationException("Database commit failed.");
            context.StagedPackageRepository
                .Setup(x => x.CommitChangesAsync())
                .ThrowsAsync(expected);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.Target.HandleAsync(context.Message));

            Assert.Same(expected, actual);
            Assert.Equal(StagedPackageStatus.Promoting, context.StagedPackage.Status);
            context.PackageFileStorageService.Verify(
                x => x.DeleteFileAsync(
                    CoreConstants.Folders.PackagesFolderName,
                    "example.package.1.2.3.nupkg"),
                Times.Once);
        }

        [Fact]
        public async Task KeepsPromotingWhenPackageCopyFails()
        {
            var context = new TestContext();
            var expected = new InvalidOperationException("Package copy failed.");
            context.PackageFileStorageService
                .Setup(x => x.CopyFileAsync(
                    context.SourceUri,
                    CoreConstants.Folders.PackagesFolderName,
                    "example.package.1.2.3.nupkg",
                    It.IsAny<IAccessCondition>()))
                .ThrowsAsync(expected);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.Target.HandleAsync(context.Message));

            Assert.Same(expected, actual);
            Assert.Equal(StagedPackageStatus.Promoting, context.StagedPackage.Status);
            context.PackageFileStorageService.Verify(
                x => x.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        public enum InvalidState
        {
            MissingValidatedBlob,
            OwnerNoLongerOwnsPackage,
        }

        private class TestContext
        {
            public TestContext()
            {
                Content = Encoding.UTF8.GetBytes("validated package content");
                SourceUri = new Uri("https://example.test/staging/validated.nupkg");
                PromotionId = Guid.NewGuid();
                var owner = new User { Key = 23, Username = "owner" };
                Package = new Package
                {
                    Key = 11,
                    NormalizedVersion = "1.2.3",
                    PackageStatusKey = PackageStatus.Staged,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "Example.Package",
                        Owners = new List<User> { owner },
                        Packages = new List<Package>(),
                    },
                };
                Package.PackageRegistration.Packages.Add(Package);
                StagedPackage = new StagedPackage
                {
                    Key = 42,
                    PackageKey = Package.Key,
                    Package = Package,
                    OwnerKey = owner.Key,
                    Owner = owner,
                    Status = StagedPackageStatus.Promoting,
                    ActivePromotionId = PromotionId,
                    ValidatedBlobPath = "example.package/1.2.3/validated.nupkg",
                    ValidatedBlobETag = "\"validated\"",
                    UploadedBlobPath = "example.package/1.2.3/uploaded.nupkg",
                    UploadedBlobETag = "\"uploaded\"",
                    UploadHash = "upload-hash",
                };
                Message = new StagedPackagePromotionMessage(PromotionId, StagedPackage.Key);
                StagedPackages = new List<StagedPackage> { StagedPackage };

                StagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                StagedPackageRepository
                    .Setup(x => x.GetAll())
                    .Returns(() => StagedPackages.AsQueryable());
                StagedPackageRepository
                    .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                    .Returns((Func<Task> action) => action());
                StagedPackageRepository
                    .Setup(x => x.DeleteOnCommit(StagedPackage))
                    .Callback(() => StagedPackages.Remove(StagedPackage));

                PackageService = new Mock<ICorePackageService>();
                PackageService
                    .Setup(x => x.UpdatePackageStreamMetadataAsync(Package, It.IsAny<NuGetGallery.Packaging.PackageStreamMetadata>(), false))
                    .Returns<Package, NuGetGallery.Packaging.PackageStreamMetadata, bool>((package, metadata, _) =>
                    {
                        package.Hash = metadata.Hash;
                        package.HashAlgorithm = metadata.HashAlgorithm;
                        package.PackageFileSize = metadata.Size;
                        return Task.CompletedTask;
                    });
                PackageService
                    .Setup(x => x.UpdatePackageStatusAsync(Package, PackageStatus.Available, false))
                    .Returns<Package, PackageStatus, bool>((package, status, _) =>
                    {
                        package.PackageStatusKey = status;
                        return Task.CompletedTask;
                    });

                StagingBlobService = new Mock<IStagingBlobService>();
                StagingBlobService
                    .Setup(x => x.OpenPackageFileAsync(StagedPackage.ValidatedBlobPath, StagedPackage.ValidatedBlobETag))
                    .ReturnsAsync(() => new MemoryStream(Content));
                StagingBlobService
                    .Setup(x => x.GetPackageReadUriAsync(StagedPackage.ValidatedBlobPath, StagedPackage.ValidatedBlobETag))
                    .ReturnsAsync(SourceUri);

                PublicBlobProperties = new Mock<ICloudBlobProperties>();
                PublicBlobProperties.SetupProperty(x => x.CacheControl, "private");
                PackageFileStorageService = new Mock<ICoreFileStorageService>();
                PackageFileStorageService
                    .Setup(x => x.SetPropertiesAsync(
                        CoreConstants.Folders.PackagesFolderName,
                        "example.package.1.2.3.nupkg",
                        It.IsAny<Func<Lazy<Task<Stream>>, ICloudBlobProperties, Task<bool>>>()))
                    .Returns<string, string, Func<Lazy<Task<Stream>>, ICloudBlobProperties, Task<bool>>>(
                        (_, __, update) => update(null, PublicBlobProperties.Object));

                LicenseFileService = new Mock<ICoreLicenseFileService>();
                ReadmeFileService = new Mock<ICoreReadmeFileService>();
                Target = new StagedPackagePromotionMessageHandler(
                    StagedPackageRepository.Object,
                    PackageService.Object,
                    StagingBlobService.Object,
                    PackageFileStorageService.Object,
                    new PackageFileMetadataService(),
                    LicenseFileService.Object,
                    ReadmeFileService.Object);
            }

            public void Apply(InvalidState state)
            {
                switch (state)
                {
                    case InvalidState.MissingValidatedBlob:
                        StagedPackage.ValidatedBlobPath = null;
                        break;
                    case InvalidState.OwnerNoLongerOwnsPackage:
                        Package.PackageRegistration.Owners.Clear();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(state));
                }
            }

            public void VerifyNotPublished()
            {
                PackageFileStorageService.Verify(
                    x => x.CopyFileAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Never);
                StagedPackageRepository.Verify(
                    x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()),
                    Times.Never);
            }

            public byte[] Content { get; }
            public Uri SourceUri { get; }
            public Guid PromotionId { get; }
            public Package Package { get; }
            public StagedPackage StagedPackage { get; }
            public StagedPackagePromotionMessage Message { get; }
            public List<StagedPackage> StagedPackages { get; }
            public Mock<IEntityRepository<StagedPackage>> StagedPackageRepository { get; }
            public Mock<ICorePackageService> PackageService { get; }
            public Mock<IStagingBlobService> StagingBlobService { get; }
            public Mock<ICoreFileStorageService> PackageFileStorageService { get; }
            public Mock<ICloudBlobProperties> PublicBlobProperties { get; }
            public Mock<ICoreLicenseFileService> LicenseFileService { get; }
            public Mock<ICoreReadmeFileService> ReadmeFileService { get; }
            public StagedPackagePromotionMessageHandler Target { get; }
        }
    }
}
