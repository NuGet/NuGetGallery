// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGet.Services.KeyVault.Tests
{
    public class RefreshableSecretReaderFactoryFacts
    {
        public class CreateSecretReader : Facts
        {
            [Fact]
            public async Task CreatesWrapper()
            {
                var actual = Target.CreateSecretReader();

                var secret = await actual.GetSecretObjectAsync(SecretName);
                Assert.IsType<RefreshableSecretReader>(actual);
                Assert.Same(secret, Secret.Object);
                UnderlyingReader.Verify(x => x.GetSecretObjectAsync(SecretName), Times.Once);
            }
        }

        public class CreateSecretInjector : Facts
        {
            [Fact]
            public void CreatesWrapper()
            {
                var actual = Target.CreateSecretInjector(UnderlyingReader.Object);

                UnderlyingFactory.Verify(
                    x => x.CreateSecretInjector(UnderlyingReader.Object),
                    Times.Once);
            }
        }

        public class RefreshAsync : Facts
        {
            [Fact]
            public async Task RefreshesSecrets()
            {
                var reader = Target.CreateSecretReader();
                await reader.GetSecretAsync(SecretName);
                UnderlyingReader.Invocations.Clear();

                await Target.RefreshAsync(CancellationToken.None);

                UnderlyingReader.Verify(x => x.GetSecretObjectAsync(SecretName), Times.Once);
            }
        }

        public class Settings : Facts
        {
            [Fact]
            public async Task AffectCreatedReaders()
            {
                var actual = Target.CreateSecretReader();
                Settings.BlockUncachedReads = true;

                await Assert.ThrowsAsync<InvalidOperationException>(() => actual.GetSecretAsync(SecretName));
            }
        }

        public abstract class Facts
        {
            public Facts()
            {
                UnderlyingFactory = new Mock<ISecretReaderFactory>();
                Settings = new RefreshableSecretReaderSettings();

                SecretName = "secret";
                UnderlyingReader = new Mock<ISecretReader>();
                SecretInjector = new Mock<ISecretInjector>();
                Secret = new Mock<ISecret>();

                UnderlyingFactory
                    .Setup(x => x.CreateSecretReader())
                    .Returns(() => UnderlyingReader.Object);
                UnderlyingFactory
                    .Setup(x => x.CreateSecretInjector(It.IsAny<ISecretReader>()))
                    .Returns(() => SecretInjector.Object);
                UnderlyingReader
                    .Setup(x => x.GetSecretObjectAsync(It.IsAny<string>()))
                    .ReturnsAsync(() => Secret.Object);

                Target = new RefreshableSecretReaderFactory(
                    UnderlyingFactory.Object,
                    Settings);
            }

            public Mock<ISecretReaderFactory> UnderlyingFactory { get; }
            public RefreshableSecretReaderSettings Settings { get; }
            public string SecretName { get; }
            public Mock<ISecretReader> UnderlyingReader { get; }
            public Mock<ISecretInjector> SecretInjector { get; }
            public Mock<ISecret> Secret { get; }
            public RefreshableSecretReaderFactory Target { get; }
        }
    }
}
