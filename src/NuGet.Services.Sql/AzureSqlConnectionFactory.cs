// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.SqlClient;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using NuGet.Services.KeyVault;

namespace NuGet.Services.Sql
{
    public class AzureSqlConnectionFactory : ISqlConnectionFactory
    {
        private const string AzureSqlResourceId = "https://database.windows.net/";

        private AzureSqlConnectionStringBuilder ConnectionStringBuilder { get; }

        private ISecretInjector SecretInjector { get; }

        #region SqlConnectionStringBuilder properies

        public string ApplicationName => ConnectionStringBuilder.Sql.ApplicationName;

        public int ConnectRetryInterval => ConnectionStringBuilder.Sql.ConnectRetryInterval;

        public string DataSource => ConnectionStringBuilder.Sql.DataSource;

        public string InitialCatalog => ConnectionStringBuilder.Sql.InitialCatalog;

        #endregion

        public AzureSqlConnectionFactory(string connectionString, ISecretInjector secretInjector)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException("Value cannot be null or empty", nameof(connectionString));
            }

            ConnectionStringBuilder = new AzureSqlConnectionStringBuilder(connectionString);
            SecretInjector = secretInjector ?? throw new ArgumentNullException(nameof(secretInjector));
        }

        public async Task<SqlConnection> CreateAsync()
        {
            try
            {
                return await ConnectAsync();
            }
            catch (Exception e) when (IsAdalException(e))
            {
                // SqlConnection.OpenAsync already contains retry logic. Keeping a retry here on our side
                // in case secrets are refreshed at runtime.
                await Task.Delay(ConnectRetryInterval * 1000);

                return await ConnectAsync();
            }
        }

        private async Task<SqlConnection> ConnectAsync()
        {
            var connectionString = await SecretInjector.InjectAsync(ConnectionStringBuilder.ConnectionString);
            var connection = new SqlConnection(connectionString);

            if (!string.IsNullOrWhiteSpace(ConnectionStringBuilder.AadAuthority))
            {
                var certificateData = await SecretInjector.InjectAsync(ConnectionStringBuilder.AadCertificate);
                if (!string.IsNullOrEmpty(certificateData))
                {
                    var certificate = SecretToCertificate(certificateData);

                    connection.AccessToken = await GetAccessTokenAsync(
                        ConnectionStringBuilder.AadAuthority,
                        ConnectionStringBuilder.AadClientId,
                        ConnectionStringBuilder.AadSendX5c,
                        certificate);
                }
            }

            await OpenConnectionAsync(connection);

            return connection;
        }

        protected virtual async Task<string> GetAccessTokenAsync(string authority, string clientId, bool sendX5c, X509Certificate2 certificate)
        {
            var clientAssertion = new ClientAssertionCertificate(clientId, certificate);
            var authenticationContext = new AuthenticationContext(authority);

            var result = await authenticationContext.AcquireTokenAsync(AzureSqlResourceId, clientAssertion, sendX5c);

            return result.AccessToken;
        }

        protected virtual Task OpenConnectionAsync(SqlConnection sqlConnection)
        {
            return sqlConnection.OpenAsync();
        }

        protected virtual X509Certificate2 SecretToCertificate(string certificateData)
        {
            return new X509Certificate2(Convert.FromBase64String(certificateData), string.Empty);
        }

        private static bool IsAdalException(Exception e)
        {
            return e is AdalException || (e.InnerException != null && IsAdalException(e.InnerException));
        }
    }
}
