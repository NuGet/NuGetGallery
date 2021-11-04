// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace NuGet.Services.KeyVault.Tests
{
    public class SecretInjectorFacts
    {
        [Fact]
        public void TryInjectCachedReturnsNullIfUnderlyingReaderIsNotCaching()
        {
            var readerMock = new Mock<ISecretReader>();
            var injector = new SecretInjector(readerMock.Object);

            var success = injector.TryInjectCached("$$secretname$$", out var result);

            Assert.False(success);
            Assert.Null(result);
        }

        [Fact]
        public void TryInjectCachedWithLoggerReturnsNullIfUnderlyingReaderIsNotCaching()
        {
            var readerMock = new Mock<ISecretReader>();
            var injector = new SecretInjector(readerMock.Object);

            var success = injector.TryInjectCached("$$secretname$$", Mock.Of<ILogger>(), out var result);

            Assert.False(success);
            Assert.Null(result);
        }

        [Fact]
        public void TryInjectCachedReturnsInputStringIfNoSecrets()
        {
            const string inputString = "no_secrets";
            var readerMock = new Mock<ISecretReader>();
            var injector = new SecretInjector(readerMock.Object);

            var success = injector.TryInjectCached(inputString, out var result);

            Assert.True(success);
            Assert.Equal(inputString, result);
        }

        [Fact]
        public void TryInjectCachedWithLoggerReturnsInputStringIfNoSecrets()
        {
            const string inputString = "no_secrets";
            var readerMock = new Mock<ISecretReader>();
            var injector = new SecretInjector(readerMock.Object);

            var success = injector.TryInjectCached(inputString, Mock.Of<ILogger>(), out var result);

            Assert.True(success);
            Assert.Equal(inputString, result);
        }

        [Fact]
        public void TryInjectCachedReturnsNullIfSecretExpired()
        {
            const string secretName = "secretName";
            const string inputString = "$$" + secretName + "$$";
            string nothing = null;
            var readerMock = new Mock<ICachingSecretReader>();
            readerMock
                .Setup(r => r.TryGetCachedSecret(secretName, out nothing))
                .Returns(false);
            var injector = new SecretInjector(readerMock.Object);

            var success = injector.TryInjectCached(inputString, out var result);

            Assert.False(success);
            Assert.Null(result);
        }
        
        [Fact]
        public void TryInjectCachedWithLoggerReturnsNullIfSecretExpired()
        {
            const string secretName = "secretName";
            const string inputString = "$$" + secretName + "$$";
            string nothing = null;
            var readerMock = new Mock<ICachingSecretReader>();
            readerMock
                .Setup(r => r.TryGetCachedSecret(secretName, It.IsAny<ILogger>(), out nothing))
                .Returns(false);
            var injector = new SecretInjector(readerMock.Object);

            var success = injector.TryInjectCached(inputString, Mock.Of<ILogger>(), out var result);

            Assert.False(success);
            Assert.Null(result);
        }

        [Fact]
        public void TryInjectCachedInjects()
        {
            const string secretName = "secretName";
            const string inputString = "$$" + secretName + "$$";
            string secretValue = "secretValue";
            var readerMock = new Mock<ICachingSecretReader>();
            readerMock
                .Setup(r => r.TryGetCachedSecret(secretName, It.IsAny<ILogger>(), out secretValue))
                .Returns(true);
            var injector = new SecretInjector(readerMock.Object);

            var success = injector.TryInjectCached(inputString, out var result);

            Assert.True(success);
            Assert.Equal(secretValue, result);
        }

        [Fact]
        public void TryInjectCachedWithLoggerInjects()
        {
            const string secretName = "secretName";
            const string inputString = "$$" + secretName + "$$";
            string secretValue = "secretValue";
            var readerMock = new Mock<ICachingSecretReader>();
            readerMock
                .Setup(r => r.TryGetCachedSecret(secretName, It.IsAny<ILogger>(), out secretValue))
                .Returns(true);
            var injector = new SecretInjector(readerMock.Object);

            var success = injector.TryInjectCached(inputString, Mock.Of<ILogger>(), out var result);

            Assert.True(success);
            Assert.Equal(secretValue, result);
        }
    }
}
