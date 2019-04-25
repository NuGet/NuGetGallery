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
using System.Data.Entity.Migrations.Infrastructure;

namespace NuGetGallery.DatabaseMigrationTools
{
    class Job : JsonConfigurationJob
    {
        public override async Task Run()
        {
            using (var sqlConnection = await _serviceProvider.GetRequiredService<ISqlConnectionFactory<GalleryDbConfiguration>>().CreateAsync())
            {
                var accessToken = sqlConnection.AccessToken;
                DbContextFactory.EntitiesContextFactory = () =>
                {
                    if (sqlConnection.State == ConnectionState.Closed)
                    {
                        // Reset the access token if the connection is closed to ensure connection authorization.
                        sqlConnection.AccessToken = accessToken;
                    }

                    return new EntitiesContext(sqlConnection, readOnly: false);
                };

                Logger.LogInformation("Initializing DbMigrator...");
                var migrationsConfiguration = new MigrationsConfiguration();
                var migrator = new DbMigrator(migrationsConfiguration);
                var migratorForScripting = new DbMigrator(migrationsConfiguration);

                ExecuteDatabaseMigration(migrator, migratorForScripting, sqlConnection, accessToken);
            }
        }

        private void ExecuteDatabaseMigration(DbMigrator migrator, DbMigrator migratorForScripting, SqlConnection sqlConnection, string accessToken)
        {
            OverWriteSqlConnection(migrator, sqlConnection, accessToken);
            OverWriteSqlConnection(migratorForScripting, sqlConnection, accessToken);

            var pendingMigrations = migrator.GetPendingMigrations();
            if (pendingMigrations.Count() > 0)
            {
                Logger.LogInformation("Target database is: {DataSource}/{Database}", sqlConnection.DataSource, sqlConnection.Database);
                Logger.LogInformation("Applying pending migrations: \n {PendingMigrations}", String.Join("\n", pendingMigrations));

                var migratorScripter = new MigratorScriptingDecorator(migratorForScripting);
                var migrationScripts = migratorScripter.ScriptUpdate(null, null);
                Logger.LogInformation("Applying explicit migration SQL scripts: \n {migrationScripts}", migrationScripts);

                try
                {
                    Logger.LogInformation("Executing migrations...");
                    migrator.Update();
                }
                catch (Exception e)
                {
                    Logger.LogError(0, e, "Failed to execute migrations on the target database {DataSource}/{Database}", sqlConnection.DataSource, sqlConnection.Database);
                    throw;
                }

                Logger.LogInformation("Finished executing {pendingMigrationsCount} migrations successfully.", pendingMigrations.Count());
            }
            else
            {
                Logger.LogInformation("There are no pending migrations to execute.");
            }
        }

        // Overwrite the database connection of DbMigrator.
        // Hit the bug:  https://github.com/aspnet/EntityFramework6/issues/522
        // Consider deleting/updating this section when the new Entity Framework 6.3 or higher version is released.
        private void OverWriteSqlConnection(DbMigrator migrator, SqlConnection sqlConnection, string accessToken)
        {
            var historyRepository = typeof(DbMigrator).GetField(
                "_historyRepository",
                BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(migrator);

            var connectionField = historyRepository.GetType().BaseType.GetField(
                "_existingConnection",
                BindingFlags.NonPublic | BindingFlags.Instance);

            sqlConnection.AccessToken = accessToken;
            connectionField.SetValue(historyRepository, sqlConnection);
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
        }
    }
}
