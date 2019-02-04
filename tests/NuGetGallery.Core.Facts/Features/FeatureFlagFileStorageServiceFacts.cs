// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace NuGetGallery.Features
{
    public class FeatureFlagFileStorageServiceFacts
    {
        public class GetAsync : FactsBase
        {
            public async Task DeserializesFlags()
            {
                // Arrange
                // TODO
                _storage
                    .Setup(s => s.GetFileAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName))
                    .ReturnsAsync(BuildStream("{}"));

                // Act
                var result = await _target.GetAsync();

                // Assert
                
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
                // TODO
                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName, null))
                    .ReturnsAsync(BuildFileReference("foo", "bar"));

                // Act
                var result = await _target.GetReferenceAsync();

                // Assert
                Assert.Equal("foo", result.Flags);
                Assert.Equal("bar", result.ContentId);
            }
        }
    
        public class TrySaveAsync : FactsBase
        {

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
