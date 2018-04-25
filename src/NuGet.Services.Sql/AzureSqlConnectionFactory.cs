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

        private const int RetryIntervalInMilliseconds = 250;

        public AzureSqlConnectionStringBuilder ConnectionStringBuilder { get; }

        private ISecretReader SecretReader { get; }

        public ICachingSecretReader CachingSecretReader { get; }

        public ISecretInjector SecretInjector { get; }

        public AzureSqlConnectionFactory(string connectionString, ISecretReader secretReader)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw Exceptions.ArgumentNullOrEmpty(nameof(connectionString));
            }

            ConnectionStringBuilder = new AzureSqlConnectionStringBuilder(connectionString);
            SecretReader = secretReader ?? throw new ArgumentNullException(nameof(secretReader));

            CachingSecretReader = secretReader as ICachingSecretReader;
            SecretInjector = new SecretInjector(secretReader);
        }
        
        public async Task<SqlConnection> CreateAsync()
        {
            try
            {
                return await ConnectAsync();
            }
            catch (Exception)
            {
                await Task.Delay(RetryIntervalInMilliseconds);

                return await ConnectAsync(forceRefresh: true);
            }
        }

        protected virtual async Task<SqlConnection> ConnectAsync(bool forceRefresh = false)
        {
            var connectionString = await SecretInjector.InjectAsync(ConnectionStringBuilder.ConnectionString, forceRefresh);

            var connection = new SqlConnection(connectionString);

            if (!string.IsNullOrWhiteSpace(ConnectionStringBuilder.AadAuthority))
            {
                connection.AccessToken = await GetAccessTokenAsync(forceRefresh);
            }

            await OpenConnectionAsync(connection);

            return connection;
        }

        private async Task<string> GetAccessTokenAsync(bool forceRefresh)
        {
            var password = await SecretInjector.InjectAsync(ConnectionStringBuilder.AadCertificatePassword, forceRefresh);

            // Parse certificate secret name with injector so that we can use the same syntax as string secrets.
            var certificateSecret = SecretInjector.GetSecretName(ConnectionStringBuilder.AadCertificate);
            if (forceRefresh)
            {
                CachingSecretReader?.RefreshCertificateSecret(certificateSecret);
            }
            var certificate = await SecretReader.GetCertificateSecretAsync(certificateSecret, password);

            return await AcquireTokenAsync(ConnectionStringBuilder.AadAuthority, ConnectionStringBuilder.AadClientId, certificate);
        }

        protected virtual Task OpenConnectionAsync(SqlConnection sqlConnection)
        {
            return sqlConnection.OpenAsync();
        }

        protected virtual async Task<string> AcquireTokenAsync(string authority, string clientId, X509Certificate2 certificate)
        {
            var authenticationContext = new AuthenticationContext(ConnectionStringBuilder.AadAuthority);
            var clientAssertion = new ClientAssertionCertificate(ConnectionStringBuilder.AadClientId, certificate);

            var result = await authenticationContext.AcquireTokenAsync(AzureSqlResourceId, clientAssertion);
            return result.AccessToken;
        }
    }
}
