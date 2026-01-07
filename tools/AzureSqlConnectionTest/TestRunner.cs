// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using NuGet.Services.KeyVault;
using NuGet.Services.Sql;

namespace AzureSqlConnectionTest
{
    public class TestRunner
    {
        private static readonly IReadOnlyList<string> DatabaseResourceScopes = new[] { "https://database.windows.net/.default" };

        private AzureSqlConnectionStringBuilder ConnectionString { get; }

        private KeyVaultConfiguration KeyVaultConfig { get; }

        private ICachingSecretInjector SecretInjector { get; }

        public TestRunner(string connectionString, KeyVaultConfiguration keyVaultConfig)
        {
            KeyVaultConfig = keyVaultConfig ?? throw new ArgumentNullException(nameof(keyVaultConfig));

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException("Value cannot be null or empty", nameof(connectionString));
            }

            ConnectionString = new AzureSqlConnectionStringBuilder(connectionString);
            SecretInjector = new SecretInjector(new KeyVaultReader(KeyVaultConfig));
        }

        public async Task<int> TestConnectionsAsync(
            int count,
            int durationInSeconds,
            int intervalInSeconds,
            bool persist,
            bool useAdalOnly)
        {
            LogMessage($"Test started: {count} clients, {durationInSeconds}s duration, {intervalInSeconds}s interval, persist = {persist}, adal = {useAdalOnly}{Environment.NewLine}");

            var testAsync = persist ? (Func<int, int, bool, Task<int>>)
                TestPersistentConnectionAsync
                : TestTransientConnectionAsync;

            var clients = new Task<int>[count];
            for (int i = 0; i < count; i++)
            {
                clients[i] = Task.Run(async () =>
                {
                    return await testAsync(durationInSeconds, intervalInSeconds, useAdalOnly);
                });
            }

            await Task.WhenAll(clients);

            var errorCount = clients.Sum(c => c.Result);

            LogMessage($"{Environment.NewLine}Test completed: {errorCount} error(s). Press any key to exit.");
            Console.ReadKey();

            return errorCount;
        }

        private async Task<int> TestTransientConnectionAsync(
            int durationInSeconds,
            int intervalInSeconds,
            bool useAdalOnly)
        {
            return await TestConnectionInternalAsync(
                durationInSeconds,
                intervalInSeconds,
                useAdalOnly,
                CreateNewSqlConnectionAsync);
        }

        private async Task<int> TestPersistentConnectionAsync(
            int durationInSeconds,
            int intervalInSeconds,
            bool useAdalOnly)
        {
            return await TestConnectionInternalAsync(
                durationInSeconds,
                intervalInSeconds,
                useAdalOnly,
                GetPersistentSqlConnectionAsync);
        }

        private async Task<int> TestConnectionInternalAsync(
            int durationInSeconds,
            int intervalInSeconds,
            bool useAdalOnly,
            Func<SqlConnection, bool, Task<SqlConnection>> createOrGetConnectionAsync)
        {
            var errorCount = 0;
            SqlConnection lastConnection = null;

            var instanceId = Guid.NewGuid();
            var start = DateTimeOffset.Now;
            do
            {
                try
                {
                    lastConnection = await createOrGetConnectionAsync(
                        lastConnection,
                        useAdalOnly);

                    await ConnectionTestAsync(instanceId, lastConnection);
                }
                catch (Exception e)
                {
                    errorCount++;
                    LogMessage($"Failed [{instanceId}]: {e}");
                }

                await Task.Delay(intervalInSeconds * 1000);
            }
            while (KeepAlive(start, durationInSeconds));

            if (lastConnection != null)
            {
                lastConnection.Dispose();
            }

            return errorCount;
        }

        private async Task ConnectionTestAsync(Guid instanceId, SqlConnection connection)
        {
            // AccessToken value is only available before the connection is opened.
            var token = connection.AccessToken?.GetHashCode();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            var connectionId = connection.GetHashCode();
            using (var cmd = new SqlCommand("SELECT CURRENT_USER", connection))
            {
                var result = await cmd.ExecuteScalarAsync();
                var tokenStr = token?.ToString() ?? "persisted";
                LogMessage($"Connected [{instanceId}]: {result} C:({connectionId}) T:({tokenStr})");
            }
        }

        private bool KeepAlive(DateTimeOffset start, int durationInSeconds)
        {
            return (DateTimeOffset.Now - start).TotalSeconds < durationInSeconds;
        }

        private Task<SqlConnection> CreateNewSqlConnectionAsync(
            SqlConnection lastConnection,
            bool useAdalOnly)
        {
            if (lastConnection != null)
            {
                lastConnection.Dispose();
            }

            return useAdalOnly ? CreateMsalSqlConnection() : CreateNuGetSqlConnection();
        }

        private async Task<SqlConnection> GetPersistentSqlConnectionAsync(
            SqlConnection lastConnection,
            bool useAdalOnly)
        {
            return lastConnection ?? await CreateNewSqlConnectionAsync(null, useAdalOnly);
        }

        private Task<SqlConnection> CreateNuGetSqlConnection()
        {
            var connectionFactory = new AzureSqlConnectionFactory(ConnectionString, SecretInjector);
            return connectionFactory.CreateAsync();
        }

        private async Task<SqlConnection> CreateMsalSqlConnection()
        {
            var certData = await SecretInjector.InjectAsync(ConnectionString.AadCertificate);
            var certificate = new X509Certificate2(Convert.FromBase64String(certData), string.Empty);

            var clientApp = ConfidentialClientApplicationBuilder
                .Create(ConnectionString.AadClientId)
                .WithAuthority(ConnectionString.AadAuthority)
                .WithCertificate(certificate, ConnectionString.AadSendX5c)
                .Build();

            var token = await clientApp
                .AcquireTokenForClient(DatabaseResourceScopes)
                .ExecuteAsync();

            var connection = new SqlConnection(ConnectionString.ConnectionString);
            connection.AccessToken = token.AccessToken;

            return connection;
        }

        private void LogMessage(string s)
        {
            var timestamp = DateTime.Now.ToString("s");
            Console.WriteLine($"[{timestamp}] {s}");
        }
    }
}
