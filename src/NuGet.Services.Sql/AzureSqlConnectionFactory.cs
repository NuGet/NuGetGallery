// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.KeyVault;

namespace NuGet.Services.Sql
{
    public class AzureSqlConnectionFactory : ISqlConnectionFactory
    {
        private static AccessTokenCache AccessTokenCache = new AccessTokenCache();

        private AzureSqlConnectionStringBuilder ConnectionString { get; }

        private ISecretInjector SecretInjector { get; }

        private ILogger Logger { get; }

        #region SqlConnectionStringBuilder properies

        public string ApplicationName => ConnectionString.Sql.ApplicationName;

        public int ConnectRetryInterval => ConnectionString.Sql.ConnectRetryInterval;

        public string DataSource => ConnectionString.Sql.DataSource;

        public string InitialCatalog => ConnectionString.Sql.InitialCatalog;

        public SqlConnectionStringBuilder SqlConnectionStringBuilder => ConnectionString.Sql;

        #endregion

        public AzureSqlConnectionFactory(
            AzureSqlConnectionStringBuilder connectionString,
            ISecretInjector secretInjector,
            ILogger logger = null)
        {
            ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            SecretInjector = secretInjector ?? throw new ArgumentNullException(nameof(secretInjector));
            Logger = logger;
        }

        public AzureSqlConnectionFactory(string connectionString, ISecretInjector secretInjector, ILogger logger = null)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException("Value cannot be null or empty", nameof(connectionString));
            }

            ConnectionString = new AzureSqlConnectionStringBuilder(connectionString);
            SecretInjector = secretInjector ?? throw new ArgumentNullException(nameof(secretInjector));
            Logger = logger;
        }

        public Task<SqlConnection> CreateAsync()
        {
            return ConnectAsync();
        }

        public async Task<SqlConnection> OpenAsync()
        {
            var connection = await ConnectAsync();

            await OpenConnectionAsync(connection);

            return connection;
        }

        private async Task<SqlConnection> ConnectAsync()
        {
            var connectionString = await SecretInjector.InjectAsync(ConnectionString.ConnectionString, Logger);
            var connection = new SqlConnection(connectionString);

            if (!string.IsNullOrWhiteSpace(ConnectionString.AadAuthority))
            {
                var clientCertificateData = await SecretInjector.InjectAsync(ConnectionString.AadCertificate, Logger);
                if (!string.IsNullOrEmpty(clientCertificateData))
                {
                    connection.AccessToken = await AcquireAccessTokenAsync(clientCertificateData);
                }
            }

            return connection;
        }

        protected virtual Task OpenConnectionAsync(SqlConnection sqlConnection)
        {
            return sqlConnection.OpenAsync();
        }

        protected virtual async Task<string> AcquireAccessTokenAsync(string clientCertificateData)
        {
            var authResult = await AccessTokenCache.GetAsync(ConnectionString, clientCertificateData, Logger);

            return authResult.AccessToken;
        }
    }
}
