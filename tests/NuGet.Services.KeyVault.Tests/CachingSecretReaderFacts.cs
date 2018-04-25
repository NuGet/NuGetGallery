// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGet.Services.KeyVault.Tests
{
    public class CachingSecretReaderFacts
    {
        public class TheGetSecretAsyncMethod
        {
            [Fact]
            public async Task CacheIsUsedIfNotPastInterval()
            {
                // Arrange
                var secretReader = new TestCachingSecretReader();

                // Act
                var value1 = await secretReader.GetSecretAsync(TestCachingSecretReader.SecretName);
                var value2 = await secretReader.GetSecretAsync(TestCachingSecretReader.SecretName);

                // Assert
                secretReader.MockSecretReader.Verify(x => x.GetSecretAsync(TestCachingSecretReader.SecretName), Times.Once);
                Assert.Equal(TestCachingSecretReader.SecretValue, value1);
                Assert.Equal(value1, value2);
            }

            [Fact]
            public async Task CacheIsRefreshedIfPastInterval()
            {
                // Arrange
                var secretReader = new TestCachingSecretReader();

                // Act
                var value1 = await secretReader.GetSecretAsync(TestCachingSecretReader.SecretName);
                secretReader.SecretsOutdated = true;
                var value2 = await secretReader.GetSecretAsync(TestCachingSecretReader.SecretName);

                // Assert
                secretReader.MockSecretReader.Verify(x => x.GetSecretAsync(TestCachingSecretReader.SecretName), Times.Exactly(2));
                Assert.Equal(TestCachingSecretReader.SecretValue, value1);
                Assert.Equal(value1, value2);
            }
        }

        public class TheGetCertificateSecretAsyncMethod
        {
            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public void ThrowsIfSecretNameIsNullOrEmpty(string secretName)
            {
                // Arrange
                var secretReader = new TestCachingSecretReader();

                // Act & Assert
                Assert.ThrowsAsync<ArgumentException>(async () => await secretReader.GetCertificateSecretAsync(secretName, "password"));
            }

            [Fact]
            public void ThrowsIfPasswordIsNull()
            {
                // Arrange
                var secretReader = new TestCachingSecretReader();

                // Act & Assert
                Assert.ThrowsAsync<ArgumentException>(async () => await secretReader.GetCertificateSecretAsync(TestCachingSecretReader.CertSecretName, null));
            }

            [Fact]
            public async Task CacheIsUsedIfNotPastInterval()
            {
                // Arrange
                var secretReader = new TestCachingSecretReader();

                // Act
                var value1 = await secretReader.GetCertificateSecretAsync(
                    TestCachingSecretReader.CertSecretName,
                    TestCachingSecretReader.CertSecretPassword);
                var value2 = await secretReader.GetCertificateSecretAsync(
                    TestCachingSecretReader.CertSecretName,
                    TestCachingSecretReader.CertSecretPassword);

                // Assert
                secretReader.MockSecretReader.Verify(x => x.GetCertificateSecretAsync(
                    TestCachingSecretReader.CertSecretName,
                    TestCachingSecretReader.CertSecretPassword),
                    Times.Exactly(1));
                Assert.Equal(TestCachingSecretReader.CertSecretValue, value1);
                Assert.Equal(value1, value2);
            }

            [Fact]
            public async Task CacheIsRefreshedIfPastInterval()
            {
                // Arrange
                var secretReader = new TestCachingSecretReader();

                // Act
                var value1 = await secretReader.GetCertificateSecretAsync(
                    TestCachingSecretReader.CertSecretName,
                    TestCachingSecretReader.CertSecretPassword);

                secretReader.SecretsOutdated = true;

                var value2 = await secretReader.GetCertificateSecretAsync(
                    TestCachingSecretReader.CertSecretName,
                    TestCachingSecretReader.CertSecretPassword);

                // Assert
                secretReader.MockSecretReader.Verify(x => x.GetCertificateSecretAsync(
                    TestCachingSecretReader.CertSecretName,
                    TestCachingSecretReader.CertSecretPassword),
                    Times.Exactly(2));

                Assert.Equal(TestCachingSecretReader.CertSecretValue, value1);
                Assert.Equal(value1, value2);
            }
        }

        public class TheRefreshSecretMethod
        {
            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public void ThrowsIfSecretNameIsNullOrEmpty(string secretName)
            {
                // Arrange
                var secretReader = new TestCachingSecretReader();

                // Act & Assert
                Assert.Throws<ArgumentException>(() => secretReader.RefreshSecret(secretName));
            }

            [Fact]
            public void ReturnsFalseIfSecretNotInCache()
            {
                // Arrange
                var secretReader = new TestCachingSecretReader();

                // Act & Assert
                Assert.False(secretReader.RefreshSecret("notasecret"));
            }

            [Fact]
            public async Task WhenRefreshIsCalledCacheIsNotUsed()
            {
                // Arrange
                var secretReader = new TestCachingSecretReader();

                // Act
                var value1 = await secretReader.GetSecretAsync(TestCachingSecretReader.SecretName);
                var result = secretReader.RefreshSecret(TestCachingSecretReader.SecretName);
                var value2 = await secretReader.GetSecretAsync(TestCachingSecretReader.SecretName);

                // Assert
                Assert.True(result);
                secretReader.MockSecretReader.Verify(x => x.GetSecretAsync(TestCachingSecretReader.SecretName), Times.Exactly(2));
                Assert.Equal(TestCachingSecretReader.SecretValue, value1);
                Assert.Equal(value1, value2);
            }
        }

        public class TheRefreshCertificateSecretMethod
        {
            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public void ThrowsIfSecretNameIsNullOrEmpty(string secretName)
            {
                // Arrange
                var secretReader = new TestCachingSecretReader();

                // Act & Assert
                Assert.Throws<ArgumentException>(() => secretReader.RefreshCertificateSecret(secretName));
            }

            [Fact]
            public void ReturnsFalseIfSecretNotInCache()
            {
                // Arrange
                var secretReader = new TestCachingSecretReader();

                // Act & Assert
                Assert.False(secretReader.RefreshCertificateSecret("notasecret"));
            }

            [Fact]
            public async Task WhenRefreshIsCalledCacheIsNotUsed()
            {
                // Arrange
                var secretReader = new TestCachingSecretReader();

                // Act
                var value1 = await secretReader.GetCertificateSecretAsync(
                    TestCachingSecretReader.CertSecretName,
                    TestCachingSecretReader.CertSecretPassword);

                var result = secretReader.RefreshCertificateSecret(TestCachingSecretReader.CertSecretName);

                var value2 = await secretReader.GetCertificateSecretAsync(
                    TestCachingSecretReader.CertSecretName,
                    TestCachingSecretReader.CertSecretPassword);

                // Assert
                Assert.True(result);
                secretReader.MockSecretReader.Verify(x => x.GetCertificateSecretAsync(
                    TestCachingSecretReader.CertSecretName,
                    TestCachingSecretReader.CertSecretPassword),
                    Times.Exactly(2));
                Assert.Equal(TestCachingSecretReader.CertSecretValue, value1);
                Assert.Equal(value1, value2);
            }
        }

        public class TestCachingSecretReader : CachingSecretReader
        {
            public const string SecretName = "secretname";
            public const string SecretValue = "secret";

            public const string CertSecretName = "certsecretname";
            public const string CertSecretPassword = "certsecretpass";
            public static readonly X509Certificate2 CertSecretValue = new Mock<X509Certificate2>().Object;

            public bool SecretsOutdated { get; set; }

            public Mock<ISecretReader> MockSecretReader { get; }

            public TestCachingSecretReader()
                : this(new Mock<ISecretReader>())
            {
            }

            public TestCachingSecretReader(Mock<ISecretReader> mockReader)
                : base(mockReader.Object, int.MaxValue)
            {
                MockSecretReader = mockReader ?? new Mock<ISecretReader>();

                MockSecretReader.Setup(x => x.GetSecretAsync(SecretName))
                    .Returns(Task.FromResult(SecretValue));
                MockSecretReader.Setup(x => x.GetCertificateSecretAsync(CertSecretName, CertSecretPassword))
                    .Returns(Task.FromResult(CertSecretValue));
            }

            protected override bool IsSecretOutdated<T>(ICachedSecret<T> secret)
            {
                return SecretsOutdated;
            }
        }
    }
}
