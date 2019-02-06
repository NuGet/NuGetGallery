// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Moq;
using Newtonsoft.Json;
using NuGet.Services.FeatureFlags;
using Xunit;

namespace NuGetGallery.Features
{
    public class FeatureFlagFileStorageServiceFacts
    {
        public class GetAsync : FactsBase
        {
            [Fact]
            public async Task DeserializesFlags()
            {
                // Arrange
                _storage
                    .Setup(s => s.GetFileAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName))
                    .ReturnsAsync(BuildStream(FeatureFlagJsonHelper.FullJson));

                // Act
                var result = await _target.GetAsync();

                // Assert
                Assert.Single(result.Features);
                Assert.True(result.Features.ContainsKey("NuGetGallery.Typosquatting"));
                Assert.Equal(FeatureStatus.Enabled, result.Features["NuGetGallery.Typosquatting"]);

                Assert.Single(result.Flights);
                Assert.True(result.Flights.ContainsKey("NuGetGallery.TyposquattingFlight"));
                Assert.True(result.Flights["NuGetGallery.TyposquattingFlight"].All);
                Assert.True(result.Flights["NuGetGallery.TyposquattingFlight"].SiteAdmins);
                Assert.Single(result.Flights["NuGetGallery.TyposquattingFlight"].Accounts, "a");
                Assert.Single(result.Flights["NuGetGallery.TyposquattingFlight"].Domains, "b");
            }

            [Fact]
            public async Task ThrowsOnInvalidJson()
            {
                _storage
                    .Setup(s => s.GetFileAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName))
                    .ReturnsAsync(BuildStream("Bad"));

                await Assert.ThrowsAsync<JsonReaderException>(() => _target.GetAsync());
            }
        }

        public class GetReferenceAsync : FactsBase
        {
            [Fact]
            public async Task GetsFlags()
            {
                // Arrange
                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName, null))
                    .ReturnsAsync(BuildFileReference("foo", "bar"));

                // Act
                var result = await _target.GetReferenceAsync();

                // Assert - this method does not enforce valid JSON
                Assert.Equal("foo", result.Flags);
                Assert.Equal("bar", result.ContentId);

                _storage
                    .Verify(
                        s => s.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName, null),
                        Times.Once);
            }
        }
    
        public class TrySaveAsync : FactsBase
        {
            [Fact]
            public async Task ReturnsInvalid()
            {
                // Act
                var result = await _target.TrySaveAsync("bad", "123");

                // Assert
                Assert.Equal(FeatureFlagSaveResultType.Invalid, result.Type);

                _storage.Verify(
                    s => s.SaveFileAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Never);
            }

            [Fact]
            public async Task IfStorageThrowsPreconditionFailedException_ReturnsConflict()
            {
                // Arrange
                var preconditionException = new StorageException(
                    new RequestResult
                    {
                        HttpStatusCode = (int)HttpStatusCode.PreconditionFailed
                    },
                    "Precondition failed",
                    new Exception());

                _storage.Setup(
                    s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.FeatureFlagsFileName,
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()))
                    .ThrowsAsync(preconditionException);

                // Act
                var result = await _target.TrySaveAsync(FeatureFlagJsonHelper.FullJson, "123");

                // Assert
                Assert.Equal(FeatureFlagSaveResult.Conflict, result);

                _storage.Verify(
                    s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.FeatureFlagsFileName,
                        It.IsAny<Stream>(),
                        It.Is<IAccessCondition>(c => c.IfNoneMatchETag == null && c.IfMatchETag != null)),
                    Times.Once);
            }

            [Fact]
            public async Task ReturnsOk()
            {
                // Act
                var result = await _target.TrySaveAsync(FeatureFlagJsonHelper.FullJson, "123");

                // Assert
                Assert.Equal(FeatureFlagSaveResult.Ok, result);

                _storage.Verify(
                    s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.FeatureFlagsFileName,
                        It.IsAny<Stream>(),
                        It.Is<IAccessCondition>(c => c.IfNoneMatchETag == null && c.IfMatchETag != null)),
                    Times.Once);
            }
        }

        public class FactsBase
        {
            protected readonly Mock<ICoreFileStorageService> _storage;
            protected readonly FeatureFlagFileStorageService _target;

            public FactsBase()
            {
                _storage = new Mock<ICoreFileStorageService>();
                _target = new FeatureFlagFileStorageService(_storage.Object);
            }

            protected Stream BuildStream(string content)
            {
                return new MemoryStream(Encoding.UTF8.GetBytes(content ?? ""));
            }

            protected IFileReference BuildFileReference(string content, string contentId)
            {
                return new FileReference
                {
                    Stream = BuildStream(content),
                    ContentId = contentId
                };
            }

            private class FileReference : IFileReference
            {
                public Stream Stream { get; set; }
                public string ContentId { get; set; }

                public Stream OpenRead() => Stream;
            }
        }
    }
}
