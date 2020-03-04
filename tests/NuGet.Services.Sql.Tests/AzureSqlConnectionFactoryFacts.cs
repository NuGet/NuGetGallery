// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using NuGet.Services.KeyVault;

namespace NuGet.Services.Sql.Tests
{
    public class AzureSqlConnectionFactoryFacts
    {
        public class TheConstructor
        {
            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public void WhenConnectionStringMissing_ThrowsArgumentException(string connectionString)
            {
                Assert.Throws<ArgumentException>(() => new AzureSqlConnectionFactory(
                    connectionString,
                    new Mock<ISecretInjector>().Object));
            }

            [Fact]
            public void WhenSecretInjectorIsNull_ThrowsArgumentException()
            {
                Assert.Throws<ArgumentNullException>(() => new AzureSqlConnectionFactory(
                    MockConnectionStrings.SqlConnectionString,
                    null));
            }

            [Fact]
            public void WhenLoggerIsNull_DoesNotThrowArgumentException()
            {
                new AzureSqlConnectionFactory(MockConnectionStrings.SqlConnectionString, new Mock<ISecretInjector>().Object);
            }
        }

        public class TheCreateAndOpenAsyncMethods
        {
            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenSqlConnectionString_InjectsSecrets(bool shouldOpen)
            {
                // Arrange
                var factory = new MockFactory(MockConnectionStrings.SqlConnectionString);

                // Act
                var connection = await ConnectAsync(factory, shouldOpen);

                // Assert
                factory.MockSecretReader.Verify(x => x.GetSecretAsync(It.IsAny<string>(), It.IsAny<ILogger>()), Times.Exactly(2));
                factory.MockSecretReader.Verify(x => x.GetSecretAsync("user", It.IsAny<ILogger>()), Times.Once);
                factory.MockSecretReader.Verify(x => x.GetSecretAsync("pass", It.IsAny<ILogger>()), Times.Once);

                Assert.True(connection.ConnectionString.Equals(
                    $"{MockConnectionStrings.BaseConnectionString};User ID=user;Password=pass", StringComparison.InvariantCultureIgnoreCase));

                Assert.Equal(shouldOpen, factory.Opened);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenAadConnectionString_InjectsSecrets(bool shouldOpen)
            {
                // Arrange
                var factory = new MockFactory(MockConnectionStrings.AadSqlConnectionString);

                // Act
                var connection = await ConnectAsync(factory, shouldOpen);

                // Assert
                factory.MockSecretReader.Verify(x => x.GetSecretAsync("cert", It.IsAny<ILogger>()), Times.Once);

                // Note that AAD keys are extracted for internal use only
                Assert.True(connection.ConnectionString.Equals(
                    $"{MockConnectionStrings.BaseConnectionString}", StringComparison.InvariantCultureIgnoreCase));

                Assert.Equal(shouldOpen, factory.Opened);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenSqlConnectionString_DoesNotAcquireAccessToken(bool shouldOpen)
            {
                // Arrange
                var factory = new MockFactory(MockConnectionStrings.SqlConnectionString);

                // Act
                var connection = await ConnectAsync(factory, shouldOpen);

                // Assert
                Assert.True(string.IsNullOrEmpty(connection.AccessToken));
                Assert.Equal(0, factory.AcquireAccessTokenCalls);

                Assert.Equal(shouldOpen, factory.Opened);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenAadConnectionString_AcquiresAccessToken(bool shouldOpen)
            {
                // Arrange
                var factory = new MockFactory(MockConnectionStrings.AadSqlConnectionString);

                // Act
                var connection = await ConnectAsync(factory, shouldOpen);

                // Assert
                Assert.Equal("accessToken", connection.AccessToken);
                Assert.Equal(1, factory.AcquireAccessTokenCalls);
                Assert.Equal(shouldOpen, factory.Opened);
            }

            private Task<SqlConnection> ConnectAsync(MockFactory factory, bool shouldOpen)
            {
                return shouldOpen ? factory.OpenAsync() : factory.CreateAsync();
            }
        }
    }
}
