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
        public Mock<ISecretReader> MockSecretReader { get; }

        public bool Opened { get; private set; }

        public int AcquireAccessTokenCalls { get; private set; }

        public MockFactory(string connectionString)
            : this(connectionString, CreateMockSecretReader())
        {
        }

        public MockFactory(string connectionString, Mock<ISecretReader> mockSecretReader)
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

        private static Mock<ISecretReader> CreateMockSecretReader()
        {
            var mockSecretReader = new Mock<ISecretReader>();

            mockSecretReader.Setup(x => x.GetSecretAsync(It.IsAny<string>(), It.IsAny<ILogger>()))
                .Returns<string, ILogger>((key, logger) =>
                {
                    return Task.FromResult(key.Replace("$$", string.Empty));
                })
                .Verifiable();

            return mockSecretReader;
        }
    }
}
