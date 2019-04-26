// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Reflection;
using System.Data.SqlClient;
using System.Data.Entity.Migrations;
using System.Data.Entity.Migrations.Infrastructure;
using System.ComponentModel.Design;
using System.Collections.Generic;
using NuGet.Jobs;

namespace NuGetGallery.DatabaseMigrationTools
{
    class Job : JsonConfigurationJob
    {
        private MigrationTargetDatabaseType _migrationTargetDatabaseType;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            var migrationTargetDatabase = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.MigrationTargetDatabase);
            switch (migrationTargetDatabase)
            {
                case (JobArgumentNames.GalleryDatabase):
                    _migrationTargetDatabaseType = MigrationTargetDatabaseType.GalleryDatabase;
                    break;
                case (JobArgumentNames.SupportRequestDatabase):
                    _migrationTargetDatabaseType = MigrationTargetDatabaseType.SupportRequestDatabase;
                    break;
                case (JobArgumentNames.ValidationDatabase):
                    _migrationTargetDatabaseType = MigrationTargetDatabaseType.ValidationDatabase;
                    break;
                default:
                    throw new ArgumentException("Invalidate target database for migrations: " + migrationTargetDatabase);
            }
        }

        public override async Task Run()
        {
            Logger.LogInformation("Initializing DbMigrator...");
            var migrationContextFactory = _serviceProvider.GetRequiredService<IMigrationContextFactory>();
            var migrationContext = await migrationContextFactory.CreateMigrationContext(_migrationTargetDatabaseType);

            ExecuteDatabaseMigration(migrationContext.Migrator,
                                     migrationContext.MigratorForScripting,
                                     migrationContext.SqlConnection,
                                     migrationContext.SqlConnectionAccessToken);

            migrationContext.SqlConnection.Dispose();
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
                var migrationScripts = migratorScripter.ScriptUpdate(sourceMigration: null, targetMigration: null);
                Logger.LogInformation("Applying explicit migration SQL scripts: \n {migrationScripts}", migrationScripts);

                try
                {
                    Logger.LogInformation("Executing migrations...");

                    migrator.Update();

                    Logger.LogInformation("Finished executing {pendingMigrationsCount} migrations successfully on the target database {DataSource}/{Database}",
                        pendingMigrations.Count(),
                        sqlConnection.DataSource,
                        sqlConnection.Database);
                }
                catch (Exception e)
                {
                    Logger.LogError(0, e, "Failed to execute migrations on the target database {DataSource}/{Database}",
                        sqlConnection.DataSource,
                        sqlConnection.Database);
                    throw;
                }
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
            services.AddTransient<IMigrationContextFactory, MigrationContextFactory>();
        }
    }
}
