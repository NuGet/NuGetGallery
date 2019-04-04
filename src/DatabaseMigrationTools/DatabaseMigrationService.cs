// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Sql;
using NuGetGallery;
using NuGetGallery.Configuration;
using NuGetGallery.Configuration.SecretReader;
using NuGetGallery.Migrations;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity.Core.EntityClient;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Migrations;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseMigrationTools
{
    public class StupidLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
        {
            return new MemoryStream();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var message = formatter(state, exception);
        }
    }

    class DatabaseMigrationService
    {
        public static void UpdateDatabase()
        {
            // AAD arguments
            var vaultName = "";
            var clientId = "";
            var certificateThumbprint = "";
            var connectionString = "";

            var secretReaderFactory = new SecretReaderFactory();
            var secretReader = secretReaderFactory.CreateSecretReader(vaultName, clientId, certificateThumbprint);
            var secretInjector = secretReaderFactory.CreateSecretInjector(secretReader);

            var sqlConnectionFactory = new AzureSqlConnectionFactory(connectionString, secretInjector, new StupidLogger());
            var conn = sqlConnectionFactory.CreateAsync().Result;

            using (var connection = new SqlConnection(conn.ConnectionString))
            {
                EntitiesContextFactory.Factory = () =>
                {
                    connection.AccessToken = conn.AccessToken;
                    return new EntitiesContext(connection, true);
                };

                var configuration = new MigrationsConfiguration();
                var migrator = new DbMigrator(configuration);

                var historyRepository = typeof(DbMigrator).GetField(
                                        "_historyRepository",
                                        BindingFlags.NonPublic | BindingFlags.Instance)
                                        .GetValue(migrator);

                var connectionField = historyRepository.GetType().BaseType.GetField(
                                            "_existingConnection",
                                            BindingFlags.NonPublic | BindingFlags.Instance);

                connection.AccessToken = conn.AccessToken;
                connection.Open();
                connectionField.SetValue(historyRepository, connection);

                var localMigration = migrator.GetLocalMigrations();
                var databaseMigration = migrator.GetDatabaseMigrations();
                var migrations = migrator.GetPendingMigrations();

                migrator.Update();
            }
        }
    }
}
