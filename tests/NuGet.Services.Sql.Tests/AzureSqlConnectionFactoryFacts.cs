// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
        public const string AadTenant = "TENANT";
        public const string AadClientId = "CLIENT-ID";
        public const string BaseConnectionString = "Data Source=tcp:DB.database.windows.net;Initial Catalog=DB";

        public static readonly string SqlConnectionString = $"{BaseConnectionString};User ID=$$user$$;Password=$$pass$$";
        public static readonly string AadSqlConnectionString = $"{BaseConnectionString};AadTenant={AadTenant};AadClientId={AadClientId};AadCertificate=$$cert$$;AadCertificatePassword=$$certPass$$";

        public const string TestAccessToken = "ABCDEFG";
        public static readonly X509Certificate2 TestCertificate;

        static AzureSqlConnectionFactoryFacts()
        {
            TestCertificate = new Mock<X509Certificate2>().Object;
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void WhenConnectionStringMissingThrowsArgumentException(string connectionString)
        {
            Assert.Throws<ArgumentException>(() => new AzureSqlConnectionFactory(
                connectionString,
                new Mock<ISecretReader>().Object));
        }

        public class TheCreateAsyncMethod
        {
            [Fact]
            public async Task WhenConnectFails_WillRetryWithForceRefresh()
            {
                // Arrange
                var connectCalls = new List<bool>();
                var factory = new TestAzureSqlConnectionFactory(SqlConnectionString);
                factory.MockConnectAsync = (forceRefresh) => {
                    connectCalls.Add(forceRefresh);
                    if (connectCalls.Count == 1) { throw new Exception(); }
                    return Task.FromResult(new SqlConnection());
                };

                // Act
                var connection = await factory.CreateAsync();

                // Assert
                Assert.Equal(2, connectCalls.Count);
                Assert.False(connectCalls[0]);
                Assert.True(connectCalls[1]);
            }
        }

        public class TheConnectAsyncMethod
        {
            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void WhenSqlConnectionString_InjectsSecrets(bool forceRefresh)
            {
                // Arrange
                var factory = new TestAzureSqlConnectionFactory(SqlConnectionString);

                // Act
                var connection = factory.TestConnectAsync(forceRefresh: forceRefresh).Result;

                // Assert
                factory.MockSecretReader.Verify(x => x.GetSecretAsync(It.IsAny<string>()), Times.Exactly(2));
                factory.MockSecretReader.Verify(x => x.GetSecretAsync("user"), Times.Once);
                factory.MockSecretReader.Verify(x => x.GetSecretAsync("pass"), Times.Once);

                var refreshCalls = forceRefresh ? 1 : 0;
                factory.MockSecretReader.Verify(x => x.RefreshSecret(It.IsAny<string>()), Times.Exactly(refreshCalls * 2));
                factory.MockSecretReader.Verify(x => x.RefreshSecret("user"), Times.Exactly(refreshCalls));
                factory.MockSecretReader.Verify(x => x.RefreshSecret("pass"), Times.Exactly(refreshCalls));

                Assert.True(connection.ConnectionString.Equals(
                    $"{BaseConnectionString};User ID=user;Password=pass", StringComparison.InvariantCultureIgnoreCase));
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void WhenAadConnectionString_InjectsSecrets(bool forceRefresh)
            {
                // Arrange
                var factory = new TestAzureSqlConnectionFactory(AadSqlConnectionString);

                // Act
                var connection = factory.TestConnectAsync(forceRefresh: forceRefresh).Result;

                // Assert
                factory.MockSecretReader.Verify(x => x.GetSecretAsync(It.IsAny<string>()), Times.Once);
                factory.MockSecretReader.Verify(x => x.GetSecretAsync("certPass"), Times.Once);

                factory.MockSecretReader.Verify(x => x.GetCertificateSecretAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
                factory.MockSecretReader.Verify(x => x.GetCertificateSecretAsync("cert", "certPass"), Times.Once);

                var refreshCalls = forceRefresh ? 1 : 0;
                factory.MockSecretReader.Verify(x => x.RefreshSecret(It.IsAny<string>()), Times.Exactly(refreshCalls));
                factory.MockSecretReader.Verify(x => x.RefreshSecret("certPass"), Times.Exactly(refreshCalls));

                factory.MockSecretReader.Verify(x => x.RefreshCertificateSecret(It.IsAny<string>()), Times.Exactly(refreshCalls));
                factory.MockSecretReader.Verify(x => x.RefreshCertificateSecret("cert"), Times.Exactly(refreshCalls));

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
                var connection = factory.TestConnectAsync(forceRefresh: false).Result;

                // Assert
                Assert.True(string.IsNullOrEmpty(connection.AccessToken));
                Assert.True(string.IsNullOrEmpty(factory.AcquireTokenAuthorityArgument));
                Assert.True(string.IsNullOrEmpty(factory.AcquireTokenClientIdArgument));
                Assert.Equal(null, factory.AcquireTokenCertificateArgument);
            }

            [Fact]
            public void WhenAadConnectionString_AcquiresAccessToken()
            {
                // Arrange
                var factory = new TestAzureSqlConnectionFactory(AadSqlConnectionString);

                // Act
                var connection = factory.TestConnectAsync(forceRefresh: false).Result;

                // Assert
                Assert.Equal(TestAccessToken, connection.AccessToken);
                Assert.Equal($"https://login.microsoftonline.com/{AadTenant}/v2.0", factory.AcquireTokenAuthorityArgument);
                Assert.Equal(AadClientId, factory.AcquireTokenClientIdArgument);
                Assert.Equal(TestCertificate, factory.AcquireTokenCertificateArgument);
            }
        }

        public class TestAzureSqlConnectionFactory : AzureSqlConnectionFactory
        {
            public Mock<ICachingSecretReader> MockSecretReader { get; }

            public string AcquireTokenAuthorityArgument { get; private set; }

            public string AcquireTokenClientIdArgument { get; private set; }

            public X509Certificate2 AcquireTokenCertificateArgument { get; private set; }

            public Func<bool, Task<SqlConnection>> MockConnectAsync { get; set; }

            public TestAzureSqlConnectionFactory(string connectionString)
                : this(connectionString, CreateMockSecretReader())
            {
            }

            public TestAzureSqlConnectionFactory(string connectionString, Mock<ICachingSecretReader> mockSecretReader)
             : base(connectionString, mockSecretReader.Object)
            {
                MockSecretReader = mockSecretReader;
            }

            public virtual Task<SqlConnection> TestConnectAsync(bool forceRefresh)
            {
                return base.ConnectAsync(forceRefresh);
            }

            protected override Task<SqlConnection> ConnectAsync(bool forceRefresh = false)
            {
                if (MockConnectAsync != null)
                {
                    return MockConnectAsync(forceRefresh);
                }
                return TestConnectAsync(forceRefresh);
            }

            protected override Task OpenConnectionAsync(SqlConnection sqlConnection)
            {
                return Task.CompletedTask;
            }

            protected override Task<string> AcquireTokenAsync(string authority, string clientId, X509Certificate2 certificate)
            {
                AcquireTokenAuthorityArgument = authority;
                AcquireTokenClientIdArgument = clientId;
                AcquireTokenCertificateArgument = certificate;

                return Task.FromResult(TestAccessToken);
            }

            private static Mock<ICachingSecretReader> CreateMockSecretReader()
            {
                var mockSecretReader = new Mock<ICachingSecretReader>();

                mockSecretReader.Setup(x => x.GetSecretAsync(It.IsAny<string>()))
                    .Returns<string>(key => {
                        return Task.FromResult(key.Replace("$$", ""));
                    })
                    .Verifiable();

                mockSecretReader.Setup(x => x.RefreshSecret(It.IsAny<string>()))
                    .Returns(true)
                    .Verifiable();

                mockSecretReader.Setup(x => x.GetCertificateSecretAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(Task.FromResult(TestCertificate))
                    .Verifiable();

                mockSecretReader.Setup(x => x.RefreshCertificateSecret(It.IsAny<string>()))
                    .Returns(true)
                    .Verifiable();

                return mockSecretReader;
            }
        }
    }
}
