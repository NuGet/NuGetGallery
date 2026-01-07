// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Validation;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace Validation.Common.Job.Tests.Storage
{
    public class ProcessorPackageFileServiceFacts
    {
        private readonly ILogger<ProcessorPackageFileService> _logger;
        private readonly ProcessorPackageFileService _target;
        private readonly Mock<ICoreFileStorageService> _fileStorageService;
        private readonly Mock<ISharedAccessSignatureService> _sasService;
        private readonly string _packageId;
        private readonly string _packageNormalizedVersion;
        private readonly Guid _validationId;
        private readonly string _folderName;
        private readonly string _fileName;
        private readonly Uri _packageUri;
        private readonly TimeSpan _accessDuration;
        private readonly MemoryStream _stream;

        public ProcessorPackageFileServiceFacts(ITestOutputHelper output)
        {
            _packageId = "NuGet.Versioning";
            _packageNormalizedVersion = "4.6.0-BETA";
            _validationId = new Guid("913ce2b1-66b9-4e33-8b19-2001229afc94");
            _folderName = "validation";
            _fileName = "TestProcessor/913ce2b1-66b9-4e33-8b19-2001229afc94/nuget.versioning.4.6.0-beta.nupkg";
            _packageUri = new Uri("http://example/" + _fileName + "?secret=42");
            _accessDuration = TimeSpan.FromDays(7);
            _stream = new MemoryStream(Encoding.ASCII.GetBytes("Hello, world."));

            _fileStorageService = new Mock<ICoreFileStorageService>(MockBehavior.Strict);
            _sasService = new Mock<ISharedAccessSignatureService>();

            var loggerFactory = new LoggerFactory().AddXunit(output);
            _logger = loggerFactory.CreateLogger<ProcessorPackageFileService>();

            _target = new ProcessorPackageFileService(
                _fileStorageService.Object,
                typeof(TestProcessor),
                _sasService.Object,
                _logger);
        }

        [Fact]
        public async Task GetReadAndDeleteUriAsync()
        {
            DateTimeOffset endOfAccess = default(DateTimeOffset);
            _fileStorageService
                .Setup(x => x.GetPrivilegedFileUriAsync(
                    _folderName,
                    _fileName,
                    FileUriPermissions.Read | FileUriPermissions.Delete,
                    It.IsAny<DateTimeOffset>()))
                .ReturnsAsync(_packageUri)
                .Callback<string, string, FileUriPermissions, DateTimeOffset>((_, __, ___, d) => endOfAccess = d)
                .Verifiable();

            var before = DateTimeOffset.UtcNow;
            await _target.GetReadAndDeleteUriAsync(
                _packageId,
                _packageNormalizedVersion,
                _validationId,
                sasDefinition: null);
            var after = DateTimeOffset.UtcNow;

            _fileStorageService.Verify();
            Assert.InRange(endOfAccess, before.Add(_accessDuration), after.Add(_accessDuration));
        }

        [Fact]
        public async Task GetReadAndDeleteUriAsyncWithSasDefinition()
        {
            var sasDefinition = "sasDefinition";
            var sasToken = "?sasToken";
            var uriWithSas = new Uri(_packageUri, sasToken);
            _fileStorageService
                .Setup(x => x.GetFileUriAsync(
                    _folderName,
                    _fileName))
                .ReturnsAsync(_packageUri)
                .Verifiable();
            _sasService
                .Setup(x => x.GetFromManagedStorageAccountAsync(sasDefinition))
                .ReturnsAsync(sasToken)
                .Verifiable();

            var result = await _target.GetReadAndDeleteUriAsync(
                _packageId,
                _packageNormalizedVersion,
                _validationId,
                sasDefinition);

            _fileStorageService.Verify();
            _sasService.Verify();
            Assert.Equal(uriWithSas, result);
        }

        [Fact]
        public async Task SaveAsync()
        {
            _stream.Position = 1;
            _fileStorageService
                .Setup(x => x.SaveFileAsync(
                    _folderName,
                    _fileName,
                    _stream,
                    true))
                .Returns(Task.CompletedTask)
                .Verifiable();

            await _target.SaveAsync(
                _packageId,
                _packageNormalizedVersion,
                _validationId,
                _stream);

            _fileStorageService.Verify();
            Assert.Equal(0, _stream.Position);
        }

        [ValidatorName("TestProcessor")]
        private class TestProcessor : INuGetProcessor
        {
            public Task CleanUpAsync(INuGetValidationRequest request) => throw new NotImplementedException();
            public Task<INuGetValidationResponse> GetResponseAsync(INuGetValidationRequest request) => throw new NotImplementedException();
            public Task<INuGetValidationResponse> StartAsync(INuGetValidationRequest request) => throw new NotImplementedException();
        }
    }
}
