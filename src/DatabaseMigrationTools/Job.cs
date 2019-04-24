// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Data.SqlClient;
using System.Data.Entity.Migrations;
using NuGetGallery.Migrations;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;

namespace NuGetGallery.DatabaseMigrationTools
{
    class Job : JsonConfigurationJob
    {
        public override async Task Run()
        {
            using (var sqlConnection = await _serviceProvider.GetRequiredService<ISqlConnectionFactory<GalleryDbConfiguration>>().CreateAsync())
            {
                RunDatabaseMigration(sqlConnection, sqlConnection.AccessToken);
            }
        }

        private void RunDatabaseMigration(SqlConnection sqlConnection, string accessToken)
        {
            SetDbContextFactory(sqlConnection, accessToken);
            var migrator = SetDbMigrator();

            ExecuteDatabaseMigration(migrator, sqlConnection, accessToken);
        }

        private DbMigrator SetDbMigrator()
        {
            Logger.LogInformation("Initializing DbMigrator...");
            var migrationsConfiguration = new MigrationsConfiguration();
            return new DbMigrator(migrationsConfiguration);
        }

        private void SetDbContextFactory(SqlConnection sqlConnection, string accessToken)
        {
            // Reset the access token if the connection is closed to ensure connection authorization.
            DbContextFactory.EntitiesContextFactory = () =>
            {
                if (sqlConnection.State == ConnectionState.Closed)
                {
                    sqlConnection.AccessToken = accessToken;
                }

                return new EntitiesContext(sqlConnection, true);
            };
        }

        private void ExecuteDatabaseMigration(DbMigrator migrator, SqlConnection sqlConnection, string accessToken)
        {
            // Overwrite the database connection of DbMigrator.
            // Consider updating this section when the new Entity Framework 6.3 or higher version is released.
            var historyRepository = typeof(DbMigrator).GetField(
                                    "_historyRepository",
                                    BindingFlags.NonPublic | BindingFlags.Instance)
                                    .GetValue(migrator);

            var connectionField = historyRepository.GetType().BaseType.GetField(
                                        "_existingConnection",
                                        BindingFlags.NonPublic | BindingFlags.Instance);

            sqlConnection.AccessToken = accessToken;
            sqlConnection.Open();
            connectionField.SetValue(historyRepository, sqlConnection);

            var pendingMigrations = migrator.GetPendingMigrations();
            if (pendingMigrations.Count() > 0)
            {
                Logger.LogInformation("Executing pending migrations: \n {PendingMigrations}", String.Join("\n", pendingMigrations));
                migrator.Update();
                Logger.LogInformation("Finished executing {pendingMigrationsCount} migrations successfully.", pendingMigrations.Count());
            }
            else
            {
                Logger.LogInformation("There are no pending migrations to execute.");
            }
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
        }
    }
}
