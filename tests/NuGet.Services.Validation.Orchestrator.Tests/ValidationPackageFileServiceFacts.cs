// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class ValidationPackageFileServiceFacts
    {
        private readonly PackageValidationSet _validationSet;
        private readonly string _validationContainerName;
        private readonly string _packagesContainerName;
        private readonly string _packageFileName;
        private readonly string _validationSetPackageFileName;
        private readonly Mock<ICoreFileStorageService> _fileStorageService;
        private readonly Mock<ILogger<ValidationPackageFileService>> _logger;
        private readonly ValidationPackageFileService _target;

        public ValidationPackageFileServiceFacts()
        {
            _validationSet = new PackageValidationSet
            {
                ValidationTrackingId = new Guid("0b44d53f-0689-4f82-9530-f25f26b321aa"),
                PackageId = "NuGet.Versioning",
                PackageNormalizedVersion = "4.5.0-ALPHA",
            };

            _packagesContainerName = "packages";
            _validationContainerName = "validation";
            _packageFileName = "nuget.versioning.4.5.0-alpha.nupkg";
            _validationSetPackageFileName = "validation-sets/0b44d53f-0689-4f82-9530-f25f26b321aa/nuget.versioning.4.5.0-alpha.nupkg";

            _fileStorageService = new Mock<ICoreFileStorageService>(MockBehavior.Strict);
            _logger = new Mock<ILogger<ValidationPackageFileService>>();
            _target = new ValidationPackageFileService(_fileStorageService.Object, _logger.Object);
        }

        [Fact]
        public async Task CopyValidationPackageForValidationSetAsync()
        {
            _fileStorageService
                .Setup(x => x.CopyFileAsync(
                    _validationContainerName,
                    _packageFileName,
                    _validationContainerName,
                    _validationSetPackageFileName))
                .Returns(Task.CompletedTask)
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
                    _validationSetPackageFileName))
                .Returns(Task.CompletedTask)
                .Verifiable();

            await _target.CopyPackageFileForValidationSetAsync(_validationSet);

            _fileStorageService.Verify();
        }

        [Fact]
        public async Task CopyValidationPackageToPackageFileAsync()
        {
            _fileStorageService
                .Setup(x => x.CopyFileAsync(
                    _validationContainerName,
                    _packageFileName,
                    _packagesContainerName,
                    _packageFileName))
                .Returns(Task.CompletedTask)
                .Verifiable();

            await _target.CopyValidationPackageToPackageFileAsync(_validationSet.PackageId, _validationSet.PackageNormalizedVersion);

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
            _fileStorageService
                .Setup(x => x.CopyFileAsync(
                    _validationContainerName,
                    _validationSetPackageFileName,
                    _packagesContainerName,
                    _packageFileName))
                .Returns(Task.CompletedTask)
                .Verifiable();

            await _target.CopyValidationSetPackageToPackageFileAsync(_validationSet);
            
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
            var endOfAccess = new DateTimeOffset(2018, 1, 3, 8, 30, 0, TimeSpan.Zero);
            var expected = new Uri("http://example.com/nupkg.nupkg");
            _fileStorageService
                .Setup(x => x.GetFileReadUriAsync(
                    _validationContainerName,
                    _validationSetPackageFileName,
                    endOfAccess))
                .ReturnsAsync(expected)
                .Verifiable();

            var actual = await _target.GetPackageForValidationSetReadUriAsync(_validationSet, endOfAccess);

            Assert.Equal(expected, actual);
            _fileStorageService.Verify();
        }
    }
}
