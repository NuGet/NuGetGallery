// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Moq;
using NuGet.Jobs.Validation;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;
using NuGetGallery.Packaging;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class ValidationPackageFileServiceFacts
    {
        private readonly PackageValidationSet _validationSet;
        private readonly Package _package;
        private readonly string _validationContainerName;
        private readonly string _backupContainerName;
        private readonly string _packagesContainerName;
        private readonly string _packageFileName;
        private readonly string _validationSetPackageFileName;
        private readonly string _backupFileName;
        private readonly Uri _testUri;
        private readonly string _etag;
        private readonly string _packageContent;
        private readonly MemoryStream _packageStream;
        private readonly DateTimeOffset _endOfAccess;
        private readonly Mock<ICoreFileStorageService> _fileStorageService;
        private readonly Mock<IFileDownloader> _packageDownloader;
        private readonly Mock<ITelemetryService> _telemetryService;
        private readonly Mock<ILogger<ValidationFileService>> _logger;
        private readonly ValidationFileService _target;


        public ValidationPackageFileServiceFacts()
        {
            _package = new Package
            {
                PackageRegistration = new PackageRegistration
                {
                    Id = "NuGet.Versioning",
                },
                NormalizedVersion = "4.5.0-ALPHA",
                Hash = "NzMzMS1QNENLNEczSDQ1SA==",
            };
            _validationSet = new PackageValidationSet
            {
                ValidationTrackingId = new Guid("0b44d53f-0689-4f82-9530-f25f26b321aa"),
                PackageKey = 9999,
                PackageId = _package.PackageRegistration.Id,
                PackageNormalizedVersion = _package.NormalizedVersion,
            };

            _packagesContainerName = "packages";
            _validationContainerName = "validation";
            _backupContainerName = "package-backups";
            _packageFileName = "nuget.versioning.4.5.0-alpha.nupkg";
            _validationSetPackageFileName = "validation-sets/0b44d53f-0689-4f82-9530-f25f26b321aa/nuget.versioning.4.5.0-alpha.nupkg";
            _backupFileName = "nuget.versioning/4.5.0-alpha/rQw3wx1psxXzqB8TyM3nAQlK2RcluhsNwxmcqXE2YbgoDW735o8TPmIR4uWpoxUERddvFwjgRSGw7gNPCwuvJg2..nupkg";
            _testUri = new Uri("http://example.com/nupkg.nupkg");
            _etag = "\"some-etag\"";
            _packageContent = "Hello, world.";
            _packageStream = new MemoryStream(Encoding.ASCII.GetBytes(_packageContent));
            _endOfAccess = new DateTimeOffset(2018, 1, 3, 8, 30, 0, TimeSpan.Zero);

            _fileStorageService = new Mock<ICoreFileStorageService>(MockBehavior.Strict);

            _packageDownloader = new Mock<IFileDownloader>(MockBehavior.Strict);
            _telemetryService = new Mock<ITelemetryService>(MockBehavior.Strict);
            _logger = new Mock<ILogger<ValidationFileService>>();

            _target = new ValidationFileService(
                _fileStorageService.Object,
                _packageDownloader.Object,
                _telemetryService.Object,
                _logger.Object,
                new PackageFileMetadataService());
        }

        [Fact]
        public async Task BackupPackageFileFromValidationSetPackageAsync()
        {
            DateTimeOffset? endOfAccess = null;
            _fileStorageService
                .Setup(x => x.GetFileReadUriAsync(
                    _validationContainerName,
                    _validationSetPackageFileName,
                    It.IsAny<DateTimeOffset?>()))
                .ReturnsAsync(_testUri)
                .Callback<string, string, DateTimeOffset?>((_, __, a) => endOfAccess = a)
                .Verifiable();

            _packageDownloader
                .Setup(x => x.DownloadAsync(_testUri, CancellationToken.None))
                .ReturnsAsync(() => FileDownloadResult.Ok(_packageStream))
                .Verifiable();

            _fileStorageService
                .Setup(x => x.FileExistsAsync(_backupContainerName, _backupFileName))
                .ReturnsAsync(false)
                .Verifiable();

            _fileStorageService
                .Setup(x => x.SaveFileAsync(_backupContainerName, _backupFileName, _packageStream, true))
                .Returns(Task.CompletedTask)
                .Verifiable();

            var backupDurationMetric = new Mock<IDisposable>(MockBehavior.Strict);
            backupDurationMetric
                .Setup(m => m.Dispose())
                .Verifiable();

            _telemetryService
                .Setup(t => t.TrackDurationToBackupPackage(_validationSet))
                .Returns(backupDurationMetric.Object);

            var before = DateTimeOffset.UtcNow;
            await _target.BackupPackageFileFromValidationSetPackageAsync(_validationSet);
            var after = DateTimeOffset.UtcNow;

            _fileStorageService.Verify();
            _packageDownloader.Verify();
            Assert.NotNull(endOfAccess);
            Assert.InRange(endOfAccess.Value, before.AddMinutes(10), after.AddMinutes(10));
            Assert.Throws<ObjectDisposedException>(() => _packageStream.Length);
        }

        [Fact]
        public async Task CannotBackupGenericValidationSet()
        {
            _validationSet.ValidatingType = ValidatingType.Generic;

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _target.BackupPackageFileFromValidationSetPackageAsync(_validationSet));

            Assert.Equal("validationSet", exception.ParamName);
            Assert.Contains(
                "This method is not supported for validation sets of validating type Generic",
                exception.Message);
        }

        [Fact]
        public async Task DownloadPackageFileToDiskAsync()
        {
            _fileStorageService
                .Setup(x => x.GetFileReadUriAsync(
                    _packagesContainerName,
                    _packageFileName,
                    null))
                .ReturnsAsync(_testUri)
                .Verifiable();

            _packageDownloader
                .Setup(x => x.DownloadAsync(_testUri, CancellationToken.None))
                .ReturnsAsync(() => FileDownloadResult.Ok(_packageStream))
                .Verifiable();

            var validationSet = new PackageValidationSet()
            {
                PackageNormalizedVersion = _package.NormalizedVersion,
                PackageKey = _package.Key,
                PackageId = _package.PackageRegistration.Id
            };

            var actual = await _target.DownloadPackageFileToDiskAsync(validationSet);

            Assert.Same(_packageStream, actual);
            _fileStorageService.Verify();
            _packageDownloader.Verify();
        }

        [Fact]
        public async Task CopyValidationPackageForValidationSetAsync()
        {
            _fileStorageService
                .Setup(x => x.CopyFileAsync(
                    _validationContainerName,
                    _packageFileName,
                    _validationContainerName,
                    _validationSetPackageFileName,
                    It.Is<IAccessCondition>(y => y.IfMatchETag == null && y.IfNoneMatchETag == null)))
                .ReturnsAsync(_etag)
                .Verifiable();

            await _target.CopyValidationPackageForValidationSetAsync(_validationSet);

            _fileStorageService.Verify();
        }

        [Fact]
        public async Task CopyPackageFileForValidationSetAsync()
        {
            _fileStorageService
                .Setup(x => x.CopyFileAsync(
                    _packagesContainerName,
                    _packageFileName,
                    _validationContainerName,
                    _validationSetPackageFileName,
                    It.Is<IAccessCondition>(y => y.IfMatchETag == null && y.IfNoneMatchETag == null)))
                .ReturnsAsync(_etag)
                .Verifiable();

            var actual = await _target.CopyPackageFileForValidationSetAsync(_validationSet);

            _fileStorageService.Verify();
            Assert.Equal(_etag, actual);
        }

        [Fact]
        public async Task CopyValidationPackageToPackageFileAsync()
        {
            _fileStorageService
                .Setup(x => x.CopyFileAsync(
                    _validationContainerName,
                    _packageFileName,
                    _packagesContainerName,
                    _packageFileName,
                    It.Is<IAccessCondition>(y => y.IfNoneMatchETag == "*")))
                .ReturnsAsync(_etag)
                .Verifiable();

            await _target.CopyValidationPackageToPackageFileAsync(_validationSet);

            _fileStorageService.Verify();
        }

        [Fact]
        public async Task DoesValidationSetPackageExistAsync()
        {
            _fileStorageService
                .Setup(x => x.FileExistsAsync(
                    _validationContainerName,
                    _validationSetPackageFileName))
                .ReturnsAsync(true)
                .Verifiable();

            var exists = await _target.DoesValidationSetPackageExistAsync(_validationSet);

            Assert.True(exists);
            _fileStorageService.Verify();
        }

        [Fact]
        public async Task CopyValidationSetPackageToPackageFileAsync()
        {
            var accessCondition = AccessConditionWrapper.GenerateIfMatchCondition(_etag);
            _fileStorageService
                .Setup(x => x.CopyFileAsync(
                    _validationContainerName,
                    _validationSetPackageFileName,
                    _packagesContainerName,
                    _packageFileName,
                    accessCondition))
                .ReturnsAsync(_etag)
                .Verifiable();

            await _target.CopyValidationSetPackageToPackageFileAsync(_validationSet, accessCondition);

            _fileStorageService.Verify();
        }

        [Fact]
        public async Task CopyPackageUrlForValidationSetAsync()
        {
            _fileStorageService
                .Setup(x => x.CopyFileAsync(
                    _testUri,
                    _validationContainerName,
                    _validationSetPackageFileName,
                    It.Is<IAccessCondition>(y => y.IfMatchETag == null && y.IfNoneMatchETag == null)))
                .Returns(Task.CompletedTask)
                .Verifiable();

            await _target.CopyPackageUrlForValidationSetAsync(_validationSet, _testUri.AbsoluteUri);

            _fileStorageService.Verify();
        }

        [Fact]
        public async Task DeletePackageForValidationSetAsync()
        {
            _fileStorageService
                .Setup(x => x.DeleteFileAsync(
                    _validationContainerName,
                    _validationSetPackageFileName))
                .Returns(Task.CompletedTask)
                .Verifiable();

            await _target.DeletePackageForValidationSetAsync(_validationSet);

            _fileStorageService.Verify();
        }

        [Fact]
        public async Task GetPackageForValidationSetReadUriAsync()
        {
            _fileStorageService
                .Setup(x => x.GetFileReadUriAsync(
                    _validationContainerName,
                    _validationSetPackageFileName,
                    _endOfAccess))
                .ReturnsAsync(_testUri)
                .Verifiable();

            var actual = await _target.GetPackageForValidationSetReadUriAsync(_validationSet, _endOfAccess);

            Assert.Equal(_testUri, actual);
            _fileStorageService.Verify();
        }

        [Fact]
        public async Task UpdatePackageBlobMetadataInValidationSetAsync()
        {
            await UpdatePackageBlobMetadataAsync(_validationContainerName,
                _validationSetPackageFileName,
                f => _target.UpdatePackageBlobMetadataInValidationSetAsync(_validationSet));
        }

        [Fact]
        public async Task UpdatePackageBlobMetadataInValidationSetAndThrowExceptionsAsync()
        {
            await UpdatePackageBlobMetadataAsync_WhenETagChangesBetweenSuccessiveReadAndWriteOperations_Throws(_validationContainerName,
                _validationSetPackageFileName,
                f => _target.UpdatePackageBlobMetadataInValidationSetAsync(_validationSet));
        }

        [Fact]
        public async Task UpdatePackageBlobMetadataInValidationAsync()
        {
            await UpdatePackageBlobMetadataAsync(_validationContainerName,
                _packageFileName,
                f => _target.UpdatePackageBlobMetadataInValidationAsync(_validationSet));
        }

        [Fact]
        public async Task UpdatePackageBlobMetadataInValidationAndThrowExceptionsAsync()
        {
            await UpdatePackageBlobMetadataAsync_WhenETagChangesBetweenSuccessiveReadAndWriteOperations_Throws(_validationContainerName,
                _packageFileName,
                f => _target.UpdatePackageBlobMetadataInValidationAsync(_validationSet));
        }

        private async Task UpdatePackageBlobMetadataAsync(string testFolderName,
            string testFileName, Func<PackageValidationSet,
            Task<PackageStreamMetadata>> UpdateBlobMetadataAsync)
        {
            const string expectedHash = "NJAwUJVdN8HOjha9VNbopjFMaPVZlAPYFef4CpiYGvVEYmafbYo5CB9KtPFXF5pG7Tj7jBb4/axBJpxZKGEY2Q==";

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("peach")))
            {
                var metadata = new Dictionary<string, string>();
                var wasUpdated = false;

                var lazyStream = new Lazy<Task<Stream>>(() => Task.FromResult<Stream>(stream));

                _fileStorageService.Setup(x => x.SetMetadataAsync(
                        It.Is<string>(folderName => folderName == testFolderName),
                        It.Is<string>(fileName => fileName == testFileName),
                        It.IsNotNull<Func<Lazy<Task<Stream>>, IDictionary<string, string>, Task<bool>>>()))
                    .Callback<string, string, Func<Lazy<Task<Stream>>, IDictionary<string, string>, Task<bool>>>(
                        (folderName, fileName, updateMetadataAsync) =>
                        {
                            wasUpdated = updateMetadataAsync(lazyStream, metadata).Result;
                        })
                    .Returns(Task.CompletedTask);

                _telemetryService.Setup(
                    x => x.TrackDurationToHashPackage(
                        _package.PackageRegistration.Id,
                        _package.NormalizedVersion,
                        _validationSet.ValidationTrackingId,
                        stream.Length,
                        CoreConstants.Sha512HashAlgorithmId,
                        "System.IO.MemoryStream"))
                    .Returns(Mock.Of<IDisposable>());

                var streamMetadata = await UpdateBlobMetadataAsync(_validationSet);

                Assert.True(wasUpdated);
                Assert.Single(metadata);
                Assert.Equal(expectedHash, metadata[CoreConstants.Sha512HashAlgorithmId]);
                Assert.NotNull(streamMetadata);
                Assert.Equal(stream.Length, streamMetadata.Size);
                Assert.Equal(expectedHash, streamMetadata.Hash);
                Assert.Equal(CoreConstants.Sha512HashAlgorithmId, streamMetadata.HashAlgorithm);

                _fileStorageService.VerifyAll();
                _telemetryService.VerifyAll();
            }
        }

        private async Task UpdatePackageBlobMetadataAsync_WhenETagChangesBetweenSuccessiveReadAndWriteOperations_Throws(string testFolderName,
            string testFileName,
            Func<PackageValidationSet, Task<PackageStreamMetadata>> UpdateBlobMetadataAsync)
        {
            const string expectedHash = "NJAwUJVdN8HOjha9VNbopjFMaPVZlAPYFef4CpiYGvVEYmafbYo5CB9KtPFXF5pG7Tj7jBb4/axBJpxZKGEY2Q==";

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("peach")))
            {
                var lazyStream = new Lazy<Task<Stream>>(() => Task.FromResult<Stream>(stream));
                var metadata = new Dictionary<string, string>();
                var wasUpdated = false;

                _fileStorageService.Setup(x => x.SetMetadataAsync(
                        It.Is<string>(folderName => folderName == testFolderName),
                        It.Is<string>(fileName => fileName == testFileName),
                        It.IsNotNull<Func<Lazy<Task<Stream>>, IDictionary<string, string>, Task<bool>>>()))
                    .Callback<string, string, Func<Lazy<Task<Stream>>, IDictionary<string, string>, Task<bool>>>(
                        (folderName, fileName, updateMetadataAsync) =>
                        {
                            wasUpdated = updateMetadataAsync(lazyStream, metadata).Result;
                        })
                    .ThrowsAsync(new StorageException("The remote server returned an error: (412) The condition specified using HTTP conditional header(s) is not met."));

                _telemetryService.Setup(
                    x => x.TrackDurationToHashPackage(
                        _package.PackageRegistration.Id,
                        _package.NormalizedVersion,
                        _validationSet.ValidationTrackingId,
                        stream.Length,
                        CoreConstants.Sha512HashAlgorithmId,
                        "System.IO.MemoryStream"))
                    .Returns(Mock.Of<IDisposable>());

                await Assert.ThrowsAsync<StorageException>(() => UpdateBlobMetadataAsync(_validationSet));

                Assert.True(wasUpdated);
                Assert.Single(metadata);
                Assert.Equal(expectedHash, metadata[CoreConstants.Sha512HashAlgorithmId]);

                _fileStorageService.VerifyAll();
                _telemetryService.VerifyAll();
            }
        }
    }
}