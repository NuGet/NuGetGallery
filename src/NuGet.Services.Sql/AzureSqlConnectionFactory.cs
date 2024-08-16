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

        private ICachingSecretInjector SecretInjector { get; }

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
            ICachingSecretInjector secretInjector,
            ILogger logger = null)
        {
            ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            SecretInjector = secretInjector ?? throw new ArgumentNullException(nameof(secretInjector));
            Logger = logger;
        }

        public AzureSqlConnectionFactory(string connectionString, ICachingSecretInjector secretInjector, ILogger logger = null)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException("Value cannot be null or empty", nameof(connectionString));
            }

            ConnectionString = new AzureSqlConnectionStringBuilder(connectionString);
            SecretInjector = secretInjector ?? throw new ArgumentNullException(nameof(secretInjector));
            Logger = logger;
        }

        public bool TryCreate(out SqlConnection sqlConnection)
        {
            sqlConnection = null;
            if (!SecretInjector.TryInjectCached(ConnectionString.ConnectionString, Logger, out var connectionString))
            {
                return false;
            }
            string accessToken = null;

            if (!string.IsNullOrWhiteSpace(ConnectionString.AadAuthority))
            {
                if (!SecretInjector.TryInjectCached(ConnectionString.AadCertificate, Logger, out var clientCertificateData))
                {
                    return false;
                }
                if (!string.IsNullOrEmpty(clientCertificateData))
                {
                    if (!TryAcquireAccessToken(clientCertificateData, out var token))
                    {
                        return false;
                    }
                    accessToken = token;
                }
            }

            sqlConnection = new SqlConnection(connectionString);
            if (accessToken != null)
            {
                sqlConnection.AccessToken = accessToken;
            }
            return true;
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
            string accessToken = null;

            if (!string.IsNullOrWhiteSpace(ConnectionString.AadAuthority))
            {
                var clientCertificateData = await SecretInjector.InjectAsync(ConnectionString.AadCertificate, Logger);
                if (!string.IsNullOrEmpty(clientCertificateData))
                {
                    accessToken = await AcquireAccessTokenAsync(clientCertificateData);
                }
            }

            var connection = new SqlConnection(connectionString);
            if (accessToken != null)
            {
                connection.AccessToken = accessToken;
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

        protected virtual bool TryAcquireAccessToken(string clientCertificateData, out string accessToken)
        {
            accessToken = null;
            if (AccessTokenCache.TryGetCached(ConnectionString, clientCertificateData, out var authenticationResult, Logger))
            {
                accessToken = authenticationResult.AccessToken;
                return true;
            }

            return false;
        }
    }
}
