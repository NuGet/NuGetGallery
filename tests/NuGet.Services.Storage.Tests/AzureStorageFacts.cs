// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Moq;
using Xunit;

namespace NuGet.Services.Storage.Tests
{
    public class AzureStorageFacts
    {
        private const string NoEmulator = "The Azure Storage emulator is not running on the CI.";
        private const string ContainerName = "test";
        private static readonly Uri BaseAddress = new Uri("http://example/dir/");
        private const string BlobName = "blob.txt";
        private const string InitialContent = "pre-existing content";

        public class Load : BaseFacts
        {
            [Theory(Skip = NoEmulator)]
            [InlineData(true)]
            [InlineData(false)]
            public async Task ProperlyRoundTripsBytes(bool compressContent)
            {
                // Arrange
                await CreateBlobAsync(_dirPath, BlobName);

                _target.CompressContent = compressContent;

                var inputBytes = new byte[] { 0x80 };
                var inputContent = new StreamStorageContent(new MemoryStream(inputBytes));

                await _target.Save(_resourceUri, inputContent, overwrite: true, cancellationToken: CancellationToken.None);

                // Act
                var response = await _target.Load(_resourceUri, CancellationToken.None);

                // Assert
                using (var stream = response.GetContentStream())
                {
                    var buffer = new MemoryStream();
                    await stream.CopyToAsync(buffer);
                    var outputBytes = buffer.ToArray();
                    Assert.Equal(inputBytes, outputBytes);
                }
            }
        }

        public class Save : BaseFacts
        {
            [Fact(Skip = NoEmulator)]
            public async Task ThrowsWhenOverwritesAreNotDesired()
            {
                // Arrange
                await CreateBlobAsync(_dirPath, BlobName);

                var content = new StringStorageContent("new content");

                // Act & Assert
                var exception = await Assert.ThrowsAsync<Exception>(
                    () => _target.Save(_resourceUri, content, overwrite: false, cancellationToken: CancellationToken.None));

                Assert.Contains("The remote server returned an error: (409) Conflict.", exception.Message);

                var currentContent = await _target.LoadString(_resourceUri, CancellationToken.None);
                Assert.Equal(InitialContent, currentContent);
            }

            [Fact(Skip = NoEmulator)]
            public async Task AllowsOverwriteWhenDesired()
            {
                // Arrange
                await CreateBlobAsync(_dirPath, BlobName);

                var inputContent = new StringStorageContent("new content");

                // Act
                await _target.Save(_resourceUri, inputContent, overwrite: true, cancellationToken: CancellationToken.None);
                
                // Assert
                var currentContent = await _target.LoadString(_resourceUri, CancellationToken.None);
                Assert.Equal(inputContent.Content, currentContent);
            }
        }

        public class BaseFacts
        {
            protected readonly string _dirPath;
            protected readonly AzureStorage _target;
            protected readonly Uri _resourceUri;

            public BaseFacts()
            {
                _dirPath = Guid.NewGuid().ToString();
                _target = new AzureStorage(
                    CloudStorageAccount.DevelopmentStorageAccount,
                    ContainerName,
                    _dirPath,
                    BaseAddress,
                    useServerSideCopy: true,
                    initializeContainer: true,
                    logger: new Mock<ILogger<AzureStorage>>().Object);

                _resourceUri = new Uri(BaseAddress, BlobName);
            }
        }

        private static async Task CreateBlobAsync(string dirPath, string filePath)
        {
            await CreateBlobAsync(dirPath, filePath, Encoding.UTF8.GetBytes(InitialContent));
        }

        private static async Task CreateBlobAsync(string dirPath, string filePath, byte[] content)
        {
            var account = CloudStorageAccount.DevelopmentStorageAccount;
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference(ContainerName);
            var directory = container.GetDirectoryReference(dirPath);
            var blob = directory.GetBlockBlobReference(filePath);

            await blob.UploadFromByteArrayAsync(content, 0, content.Length);
        }
    }
}
