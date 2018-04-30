// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.SqlClient;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Moq;
using NuGet.Services.KeyVault;
using Xunit;

namespace NuGet.Services.Sql.Tests
{
    public class AzureSqlConnectionFactoryFacts
    {
        public const string AadTenant = "TENANT";
        public const string AadClientId = "CLIENT-ID";
        public const string BaseConnectionString = "Data Source=tcp:DB.database.windows.net;Initial Catalog=DB";

        public static readonly string SqlConnectionString = $"{BaseConnectionString};User ID=$$user$$;Password=$$pass$$";
        public static readonly string AadSqlConnectionString = $"{BaseConnectionString};AadTenant={AadTenant};AadClientId={AadClientId};AadCertificate=$$cert$$";

        public const string TestAccessToken = "ABCDEFG";
        public static readonly X509Certificate2 TestCertificate;

        static AzureSqlConnectionFactoryFacts()
        {
            TestCertificate = new Mock<X509Certificate2>().Object;
        }

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

        public class TheCreateAsyncMethod
        {
            [Fact]
            public async Task WhenConnectFails_RefreshesSecretsAndRetries()
            {
                // Arrange
                var connectCount = 0;
                var factory = new TestAzureSqlConnectionFactory(SqlConnectionString);
                factory.MockConnectAsync = async () =>
                {
                    var result = await factory.TestConnectAsync();
                    if (connectCount++ == 0) { throw new AdalException(); }
                    return result;
                };

                // Act
                var connection = await factory.CreateAsync();

                // Assert
                Assert.Equal(2, connectCount);
                factory.MockSecretReader.Verify(s => s.GetSecretAsync("user"), Times.Exactly(2));
                factory.MockSecretReader.Verify(s => s.GetSecretAsync("pass"), Times.Exactly(2));
                factory.MockSecretReader.Verify(s => s.RefreshSecret("user"), Times.Once);
                factory.MockSecretReader.Verify(s => s.RefreshSecret("pass"), Times.Once);
            }
        }

        public class TheConnectAsyncMethod
        {
            [Fact]
            public void WhenSqlConnectionString_InjectsSecrets()
            {
                // Arrange
                var factory = new TestAzureSqlConnectionFactory(SqlConnectionString);

                // Act
                var connection = factory.TestConnectAsync().Result;

                // Assert
                factory.MockSecretReader.Verify(x => x.GetSecretAsync(It.IsAny<string>()), Times.Exactly(2));
                factory.MockSecretReader.Verify(x => x.GetSecretAsync("user"), Times.Once);
                factory.MockSecretReader.Verify(x => x.GetSecretAsync("pass"), Times.Once);

                Assert.True(connection.ConnectionString.Equals(
                    $"{BaseConnectionString};User ID=user;Password=pass", StringComparison.InvariantCultureIgnoreCase));
            }

            [Fact]
            public void WhenAadConnectionString_InjectsSecrets()
            {
                // Arrange
                var factory = new TestAzureSqlConnectionFactory(AadSqlConnectionString);

                // Act
                var connection = factory.TestConnectAsync().Result;

                // Assert
                factory.MockSecretReader.Verify(x => x.GetSecretAsync("cert"), Times.Once);

                // Note that AAD keys are extracted for internal use only
                Assert.True(connection.ConnectionString.Equals(
                    $"{BaseConnectionString}", StringComparison.InvariantCultureIgnoreCase));
            }

            [Fact]
            public void WhenSqlConnectionString_DoesNotAcquireAccessToken()
            {
                // Arrange
                var factory = new TestAzureSqlConnectionFactory(SqlConnectionString);

                // Act
                var connection = factory.TestConnectAsync().Result;

                // Assert
                Assert.True(string.IsNullOrEmpty(connection.AccessToken));
                Assert.Null(factory.AcquireTokenArguments);
            }

            [Fact]
            public void WhenAadConnectionString_AcquiresAccessToken()
            {
                // Arrange
                var factory = new TestAzureSqlConnectionFactory(AadSqlConnectionString);

                // Act
                var connection = factory.TestConnectAsync().Result;

                // Assert
                Assert.Equal(TestAccessToken, connection.AccessToken);
                Assert.Equal($"https://login.microsoftonline.com/{AadTenant}/v2.0", factory.AcquireTokenArguments.Item1);
                Assert.Equal(AadClientId, factory.AcquireTokenArguments.Item2);
                Assert.True(factory.AcquireTokenArguments.Item3);
                Assert.Equal(TestCertificate, factory.AcquireTokenArguments.Item4);
            }
        }

        public class TestAzureSqlConnectionFactory : AzureSqlConnectionFactory
        {
            public Mock<ICachingSecretReader> MockSecretReader { get; }

            public Func<Task<SqlConnection>> MockConnectAsync { get; set; }

            public Tuple<string, string, bool, X509Certificate2> AcquireTokenArguments { get; private set; }

            public TestAzureSqlConnectionFactory(string connectionString)
                : this(connectionString, CreateMockSecretReader())
            {
            }

            public TestAzureSqlConnectionFactory(string connectionString, Mock<ICachingSecretReader> mockSecretReader)
             : base(connectionString, new SecretInjector(mockSecretReader.Object))
            {
                MockSecretReader = mockSecretReader;
            }

            public virtual Task<SqlConnection> TestConnectAsync()
            {
                return base.ConnectAsync();
            }

            protected override Task<SqlConnection> ConnectAsync()
            {
                if (MockConnectAsync != null)
                {
                    return MockConnectAsync();
                }
                return TestConnectAsync();
            }

            protected override Task<string> GetAccessTokenAsync(string authority, string clientId, bool sendX5c, X509Certificate2 certificate)
            {
                AcquireTokenArguments = Tuple.Create(authority, clientId, sendX5c, certificate);

                return Task.FromResult(TestAccessToken);
            }

            protected override Task OpenConnectionAsync(SqlConnection sqlConnection)
            {
                return Task.CompletedTask;
            }

            protected override X509Certificate2 SecretToCertificate(string rawData)
            {
                return TestCertificate;
            }

            private static Mock<ICachingSecretReader> CreateMockSecretReader()
            {
                var mockSecretReader = new Mock<ICachingSecretReader>();

                mockSecretReader.Setup(x => x.GetSecretAsync(It.IsAny<string>()))
                    .Returns<string>(key =>
                    {
                        return Task.FromResult(key.Replace("$$", ""));
                    })
                    .Verifiable();

                mockSecretReader.Setup(x => x.RefreshSecret(It.IsAny<string>()))
                    .Returns(true)
                    .Verifiable();

                return mockSecretReader;
            }
        }
    }
}
