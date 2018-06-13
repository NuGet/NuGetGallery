// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.SqlClient;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.KeyVault;
using Xunit;

namespace NuGet.Services.Sql.Tests
{
    public class AzureSqlConnectionFactoryFacts
    {
        public const string AadTenant = "aadTenant";
        public const string AadClientId = "aadClientId";
        public const string TestAccessToken = "aadAccessToken";

        public const string BaseConnectionString = "Data Source=tcp:DB.database.windows.net;Initial Catalog=DB";

        public static readonly string SqlConnectionString = $"{BaseConnectionString};User ID=$$user$$;Password=$$pass$$";
        public static readonly string AadSqlConnectionString = $"{BaseConnectionString};AadTenant={AadTenant};AadClientId={AadClientId};AadCertificate=$$cert$$";

        public static readonly X509Certificate2 TestCertificate;

        static AzureSqlConnectionFactoryFacts()
        {
            TestCertificate = new Mock<X509Certificate2>().Object;
        }

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
                    SqlConnectionString,
                    null));
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
                var factory = new TestAzureSqlConnectionFactory(SqlConnectionString);

                // Act
                var connection = shouldOpen ? await factory.OpenAsync() : await factory.CreateAsync();

                // Assert
                factory.MockSecretReader.Verify(x => x.GetSecretAsync(It.IsAny<string>()), Times.Exactly(2));
                factory.MockSecretReader.Verify(x => x.GetSecretAsync("user"), Times.Once);
                factory.MockSecretReader.Verify(x => x.GetSecretAsync("pass"), Times.Once);

                Assert.True(connection.ConnectionString.Equals(
                    $"{BaseConnectionString};User ID=user;Password=pass", StringComparison.InvariantCultureIgnoreCase));

                Assert.Equal(shouldOpen, factory.Opened);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenAadConnectionString_InjectsSecrets(bool shouldOpen)
            {
                // Arrange
                var factory = new TestAzureSqlConnectionFactory(AadSqlConnectionString);

                // Act
                var connection = shouldOpen ? await factory.OpenAsync() : await factory.CreateAsync();

                // Assert
                factory.MockSecretReader.Verify(x => x.GetSecretAsync("cert"), Times.Once);

                // Note that AAD keys are extracted for internal use only
                Assert.True(connection.ConnectionString.Equals(
                    $"{BaseConnectionString}", StringComparison.InvariantCultureIgnoreCase));

                Assert.Equal(shouldOpen, factory.Opened);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenSqlConnectionString_DoesNotAcquireAccessToken(bool shouldOpen)
            {
                // Arrange
                var factory = new TestAzureSqlConnectionFactory(SqlConnectionString);

                // Act
                var connection = shouldOpen ? await factory.OpenAsync() : await factory.CreateAsync();

                // Assert
                Assert.True(string.IsNullOrEmpty(connection.AccessToken));
                Assert.Null(factory.AcquireTokenArguments);

                Assert.Equal(shouldOpen, factory.Opened);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenAadConnectionString_AcquiresAccessToken(bool shouldOpen)
            {
                // Arrange
                var factory = new TestAzureSqlConnectionFactory(AadSqlConnectionString);

                // Act
                var connection = shouldOpen ? await factory.OpenAsync() : await factory.CreateAsync();

                // Assert
                Assert.Equal(TestAccessToken, connection.AccessToken);
                Assert.Equal($"https://login.microsoftonline.com/{AadTenant}/v2.0", factory.AcquireTokenArguments.Item1);
                Assert.Equal(AadClientId, factory.AcquireTokenArguments.Item2);
                Assert.True(factory.AcquireTokenArguments.Item3);
                Assert.Equal(TestCertificate, factory.AcquireTokenArguments.Item4);

                Assert.Equal(shouldOpen, factory.Opened);
            }
        }

        public class TestAzureSqlConnectionFactory : AzureSqlConnectionFactory
        {
            public Mock<ISecretReader> MockSecretReader { get; }

            public Tuple<string, string, bool, X509Certificate2> AcquireTokenArguments { get; private set; }

            public bool Opened { get; private set; }

            public TestAzureSqlConnectionFactory(string connectionString)
                : this(connectionString, CreateMockSecretReader())
            {
            }

            public TestAzureSqlConnectionFactory(string connectionString, Mock<ISecretReader> mockSecretReader)
             : base(connectionString, new SecretInjector(mockSecretReader.Object))
            {
                MockSecretReader = mockSecretReader;
            }

            protected override Task<string> GetAccessTokenAsync(string authority, string clientId, bool sendX5c, X509Certificate2 certificate)
            {
                AcquireTokenArguments = Tuple.Create(authority, clientId, sendX5c, certificate);

                return Task.FromResult(TestAccessToken);
            }

            protected override Task OpenConnectionAsync(SqlConnection sqlConnection)
            {
                Opened = true;
                return Task.CompletedTask;
            }

            protected override X509Certificate2 SecretToCertificate(string certificateData)
            {
                return TestCertificate;
            }

            private static Mock<ISecretReader> CreateMockSecretReader()
            {
                var mockSecretReader = new Mock<ISecretReader>();

                mockSecretReader.Setup(x => x.GetSecretAsync(It.IsAny<string>()))
                    .Returns<string>(key =>
                    {
                        return Task.FromResult(key.Replace("$$", string.Empty));
                    })
                    .Verifiable();

                return mockSecretReader;
            }
        }
    }
}
