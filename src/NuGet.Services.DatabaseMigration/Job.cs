// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Data.Entity.Migrations;
using System.Data.Entity.Migrations.Infrastructure;
using System.ComponentModel.Design;
using System.Collections.Generic;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;

namespace NuGet.Services.DatabaseMigration
{
    public class Job : JsonConfigurationJob
    {
        private string _migrationTargetDatabase;
        private IMigrationContextFactory _migrationContextFactory;

        private const string MigrationTargetDatabaseArgument = "MigrationTargetDatabase";
        // There is a Gallery migration file which doens't exist in the local migration folder;
        // Need to skip this migration file for the validation check.
        private const string SkipGalleryDatabaseMigrationFile = "201304262247205_CuratedPackagesUniqueIndex";

        public Job(IMigrationContextFactory migrationContextFactory)
        {
            _migrationContextFactory = migrationContextFactory;
        }

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);
            _migrationTargetDatabase = JobConfigurationManager.GetArgument(jobArgsDictionary, MigrationTargetDatabaseArgument);
        }

        public override async Task Run()
        {
            Logger.LogInformation("Initializing database migration context...");

            using (var migrationContext = await _migrationContextFactory.CreateMigrationContextAsync(_migrationTargetDatabase, _serviceProvider))
            {
                ExecuteDatabaseMigration(migrationContext.GetDbMigrator, migrationContext.SqlConnection);
            }
        }

        public void CheckIsValidMigration(List<string> databaseMigrations, List<string> localMigrations)
        {
            if (databaseMigrations == null)
            {
                throw new ArgumentNullException(nameof(databaseMigrations));
            }
            if (localMigrations == null)
            {
                throw new ArgumentNullException(nameof(localMigrations));
            }
            if (databaseMigrations.Count == 0)
            {
                throw new InvalidOperationException("Migration validation failed: Unexpected empty history of database migrations.");
            }
            if (localMigrations.Count == 0)
            {
                throw new InvalidOperationException("Migration validation failed: Unexpected empty history of local migrations.");
            }

            var databaseMigrationsCursor = 0;
            var localMigrationsCursor = 0;
            while (databaseMigrationsCursor < databaseMigrations.Count &&
                localMigrationsCursor < localMigrations.Count)
            {
                if (_migrationTargetDatabase != null &&
                    _migrationTargetDatabase.Equals(MigrationTargetDatabaseArgumentNames.GalleryDatabase) &&
                    databaseMigrations[databaseMigrationsCursor].Equals(SkipGalleryDatabaseMigrationFile))
                {
                    databaseMigrationsCursor++;
                }
                else
                {
                    if (!databaseMigrations[databaseMigrationsCursor].Equals(localMigrations[localMigrationsCursor]))
                    {
                        throw new InvalidOperationException($"Migration validation failed: Mismatch local migration file: {localMigrations[localMigrationsCursor]}" +
                            $" and database migration file: {databaseMigrations[databaseMigrationsCursor]}.");
                    }

                    localMigrationsCursor++;
                    databaseMigrationsCursor++;
                }
            }
        }

        private void ExecuteDatabaseMigration(Func<DbMigrator> getMigrator, SqlConnection sqlConnection)
        {
            var migrator = getMigrator();
            var migratorForScripting = getMigrator();

            var sqlConnectionDataSource = sqlConnection.DataSource;
            var sqlConnectionDatabase = sqlConnection.Database;

            Logger.LogInformation("Target database is: {DataSource}/{Database}.", sqlConnectionDataSource, sqlConnectionDatabase);
            var pendingMigrations = migrator.GetPendingMigrations();
            if (pendingMigrations.Count() > 0)
            {
                var databaseMigrations = migrator.GetDatabaseMigrations().ToList();
                databaseMigrations.Reverse();
                var localMigrations = migrator.GetLocalMigrations().ToList();
                try
                {
                    CheckIsValidMigration(databaseMigrations, localMigrations);
                }
                catch (Exception e)
                {
                    Logger.LogError(0, e, "Validation check of migration context failed.");
                    throw;
                }

                Logger.LogInformation("Validation check of migration context finished successfully.");
                Logger.LogInformation("Applying pending migrations: \n {PendingMigrations}.", String.Join("\n", pendingMigrations));

                var migratorScripter = new MigratorScriptingDecorator(migratorForScripting);
                var migrationScripts = migratorScripter.ScriptUpdate(sourceMigration: null, targetMigration: null);
                Logger.LogInformation("Applying explicit migration SQL scripts: \n {migrationScripts}.", migrationScripts);

                try
                {
                    Logger.LogInformation("Executing migrations...");

                    migrator.Update();

                    Logger.LogInformation("Finished executing {pendingMigrationsCount} migrations successfully on the target database {DataSource}/{Database}.",
                        pendingMigrations.Count(),
                        sqlConnectionDataSource,
                        sqlConnectionDatabase);
                }
                catch (Exception e)
                {
                    Logger.LogError(0, e, "Failed to execute migrations on the target database {DataSource}/{Database}.",
                        sqlConnectionDataSource,
                        sqlConnectionDatabase);
                    throw;
                }
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

