// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Services.Storage;
using Validation.PackageSigning.Core.Tests.TestData;
using Xunit;

namespace Validation.PackageSigning.Core.Tests
{
    public class CertificateStoreTests
    {
        public class TheExistsAsyncMethod
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task UsesExpectedFileName(bool fileExists)
            {
                // Arrange
                var certificate = TestResources.GetTestCertificate(TestResources.TrustedCARootCertificate);
                var sha256Thumbprint = certificate.ComputeSHA256Thumbprint().ToUpperInvariant();
                var expectedFileName = GetExpectedFileName(sha256Thumbprint);
                var cancellationToken = CancellationToken.None;
                var storage = new Mock<IStorage>(MockBehavior.Strict);
                storage
                    .Setup(m => m.ExistsAsync(expectedFileName, cancellationToken))
                    .ReturnsAsync(fileExists)
                    .Verifiable();

                var logger = new Mock<ILogger<CertificateStore>>(MockBehavior.Strict);
                var certificateStore = new CertificateStore(storage.Object, logger.Object);

                // Act
                var result = await certificateStore.ExistsAsync(sha256Thumbprint, cancellationToken);

                // Assert
                Assert.Equal(fileExists, result);
                storage.Verify();
            }
        }

        public class TheLoadAsyncMethod
        {
            [Fact]
            public async Task UsesExpectedFileNameAndThrowsWhenCertificateFailedToLoadFromStorage()
            {
                // Arrange
                var certificate = TestResources.GetTestCertificate(TestResources.TrustedCARootCertificate);
                var sha256Thumbprint = certificate.ComputeSHA256Thumbprint();
                var expectedFileName = GetExpectedFileName(sha256Thumbprint);
                var expectedUri = GetExpectedUri(sha256Thumbprint);
                var cancellationToken = CancellationToken.None;
                var storage = new Mock<IStorage>(MockBehavior.Strict);
                storage
                    .Setup(m => m.ResolveUri(expectedFileName))
                    .Returns(expectedUri).Verifiable();
                storage
                    .Setup(m => m.Load(expectedUri, cancellationToken))
                    .ReturnsAsync(default(StorageContent))
                    .Verifiable();

                var logger = new Mock<ILogger<CertificateStore>>(MockBehavior.Loose);
                var certificateStore = new CertificateStore(storage.Object, logger.Object);

                // Act
                Task invocation() => certificateStore.LoadAsync(sha256Thumbprint, cancellationToken);

                // Assert
                await Assert.ThrowsAsync<InvalidOperationException>(invocation);
                storage.Verify();
            }

            [Fact]
            public async Task LoadsCertificateFromStorage()
            {
                // Arrange
                var certificate = TestResources.GetTestCertificate(TestResources.TrustedCARootCertificate);
                var sha256Thumbprint = certificate.ComputeSHA256Thumbprint();
                var expectedFileName = GetExpectedFileName(sha256Thumbprint);
                var expectedUri = GetExpectedUri(sha256Thumbprint);
                var cancellationToken = CancellationToken.None;
                var storage = new Mock<IStorage>(MockBehavior.Strict);
                storage
                    .Setup(m => m.ResolveUri(expectedFileName))
                    .Returns(expectedUri).Verifiable();
                storage
                    .Setup(m => m.Load(expectedUri, cancellationToken))
                    .ReturnsAsync(new StreamStorageContent(new MemoryStream(certificate.RawData)))
                    .Verifiable();

                var logger = new Mock<ILogger<CertificateStore>>(MockBehavior.Loose);
                var certificateStore = new CertificateStore(storage.Object, logger.Object);

                // Act
                var result = await certificateStore.LoadAsync(sha256Thumbprint, cancellationToken);

                // Assert
                Assert.Equal(certificate, result);
                storage.Verify();
            }
        }

        public class TheSaveAsyncMethod
        {
            [Fact]
            public async Task SavesCertificateToStorage()
            {
                // Arrange
                var certificate = TestResources.GetTestCertificate(TestResources.TrustedCARootCertificate);
                var sha256Thumbprint = certificate.ComputeSHA256Thumbprint();
                var expectedFileName = GetExpectedFileName(sha256Thumbprint);
                var expectedUri = GetExpectedUri(sha256Thumbprint);
                var cancellationToken = CancellationToken.None;
                var content = new StreamStorageContent(new MemoryStream(certificate.RawData));

                var storage = new Mock<IStorage>(MockBehavior.Strict);
                storage
                    .Setup(m => m.ResolveUri(expectedFileName))
                    .Returns(expectedUri).Verifiable();
                storage
                    .Setup(m => m.Save(expectedUri, It.IsAny<StreamStorageContent>(), false, cancellationToken))
                    .Callback<Uri, StorageContent, bool, CancellationToken>((uri, sc, overwrite, ct) =>
                    {
                        Assert.Equal(certificate.RawData, ToByteArray(sc));
                    })
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                var logger = new Mock<ILogger<CertificateStore>>(MockBehavior.Loose);
                var certificateStore = new CertificateStore(storage.Object, logger.Object);

                // Act
                await certificateStore.SaveAsync(certificate, cancellationToken);

                // Assert
                storage.Verify();
            }
        }

        private static string GetExpectedFileName(string sha256Thumbprint)
        {
            return $"sha256/{sha256Thumbprint.ToLowerInvariant()}.cer";
        }

        private static Uri GetExpectedUri(string sha256Thumbprint)
        {
            return new Uri("http://localhost/certificates/" + GetExpectedFileName(sha256Thumbprint));
        }

        private static byte[] ToByteArray(StorageContent storageContent)
        {
            byte[] rawData;
            using (var stream = storageContent.GetContentStream())
            {
                using (var buffer = new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    rawData = buffer.ToArray();
                }
            }
            return rawData;
        }
    }
}
