// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.SqlClient;
using System.Threading.Tasks;
using Moq;
using Microsoft.Extensions.Logging;
using NuGet.Services.KeyVault;

namespace NuGet.Services.Sql.Tests
{
    public class MockFactory : AzureSqlConnectionFactory
    {
        public Mock<ICachingSecretReader> MockSecretReader { get; }

        public bool Opened { get; private set; }

        public int AcquireAccessTokenCalls { get; private set; }

        public MockFactory(string connectionString)
            : this(connectionString, CreateMockSecretReader())
        {
        }

        public MockFactory(string connectionString, Mock<ICachingSecretReader> mockSecretReader)
         : base(connectionString, new SecretInjector(mockSecretReader.Object))
        {
            MockSecretReader = mockSecretReader;
        }

        protected override Task OpenConnectionAsync(SqlConnection sqlConnection)
        {
            Opened = true;
            return Task.CompletedTask;
        }

        protected override Task<string> AcquireAccessTokenAsync(string clientCertificateData)
        {
            AcquireAccessTokenCalls++;
            return Task.FromResult("accessToken");
        }

        protected override bool TryAcquireAccessToken(string clientCertificateData, out string accessToken)
        {
            AcquireAccessTokenCalls++;
            accessToken = "accessToken";
            return true;
        }

        public delegate bool TryGetCachedSecretReturns(string secretName, ILogger logger, out string secretValue);

        private static Mock<ICachingSecretReader> CreateMockSecretReader()
        {
            var mockSecretReader = new Mock<ICachingSecretReader>();

            mockSecretReader.Setup(x => x.GetSecretAsync(It.IsAny<string>(), It.IsAny<ILogger>()))
                .Returns<string, ILogger>((key, logger) =>
                {
                    return Task.FromResult(key.Replace("$$", string.Empty));
                })
                .Verifiable();

            mockSecretReader.Setup(x => x.TryGetCachedSecret(It.IsAny<string>(), It.IsAny<ILogger>(), out It.Ref<string>.IsAny))
                .Returns(new TryGetCachedSecretReturns((string key, ILogger logger, out string secretValue) =>
                {
                    secretValue = key.Replace("$$", string.Empty);
                    return true;
                }));

            return mockSecretReader;
        }
    }
}
