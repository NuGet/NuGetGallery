// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using NuGet.Services.KeyVault;

namespace NuGet.Services.Sql
{
    public class AzureSqlConnectionFactory : ISqlConnectionFactory
    {
        private const string AzureSqlResourceId = "https://database.windows.net/";

        public AzureSqlConnectionStringBuilder ConnectionStringBuilder { get; }

        public ISecretInjector SecretInjector { get; }

        public ISecretReader SecretReader {
            get
            {
                return SecretInjector.SecretReader;
            }
        }

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
                RefreshSecrets();
                return await ConnectAsync();
            }
        }

        protected virtual async Task<SqlConnection> ConnectAsync()
        {
            var connectionString = await SecretInjector.InjectAsync(ConnectionStringBuilder.ConnectionString);
            var connection = new SqlConnection(connectionString);

            if (!string.IsNullOrWhiteSpace(ConnectionStringBuilder.AadAuthority))
            {
                var certSecret = GetSecretName(ConnectionStringBuilder.AadCertificate);
                if (!string.IsNullOrEmpty(certSecret))
                {
                    var certSecretBytes = await SecretReader.GetSecretAsync(certSecret);
                    var certificate = SecretToCertificate(certSecretBytes);

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

        protected virtual X509Certificate2 SecretToCertificate(string rawData)
        {
            return new X509Certificate2(Convert.FromBase64String(rawData), string.Empty);
        }

        private void RefreshSecrets()
        {
            var cachingSecretReader = SecretReader as ICachingSecretReader;   
            if (cachingSecretReader != null)
            {
                foreach (var secret in SecretInjector.GetSecretNames(ConnectionStringBuilder.ConnectionString))
                {
                    cachingSecretReader.RefreshSecret(secret);
                }

                if (!string.IsNullOrEmpty(ConnectionStringBuilder.AadCertificate))
                {
                    var certSecret = GetSecretName(ConnectionStringBuilder.AadCertificate);
                    if (!string.IsNullOrEmpty(certSecret))
                    {
                        cachingSecretReader.RefreshSecret(certSecret);
                    }
                }
            }
        }

        private string GetSecretName(string input)
        {
            return SecretInjector.GetSecretNames(input).SingleOrDefault();
        }

        private static bool IsAdalException(Exception e)
        {
            return (e is AdalException) ? true
                : (e.InnerException != null) ? IsAdalException(e.InnerException) : false;
        }
    }
}
