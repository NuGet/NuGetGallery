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
using NuGetGallery;
using NuGetGallery.Migrations;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;

namespace DatabaseMigrationTools
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
                        sqlConnection.AccessToken = accessToken;
                    }

                    return new EntitiesContext(sqlConnection, true);
                };

                Logger.LogInformation("Initializing DbMigrator...");
                var migrationsConfiguration = new MigrationsConfiguration();
                var migrator = new DbMigrator(migrationsConfiguration);

                ExecuteDatabaseMigration(migrator, sqlConnection, accessToken);
            }
        }

        private void ExecuteDatabaseMigration(DbMigrator migrator, SqlConnection sqlConnection, string accessToken)
        {
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
                Logger.LogInformation("Execute the pending migrations: \n {PendingMigrations}", String.Join("\n", pendingMigrations));
                migrator.Update();
                Logger.LogInformation("Finish executing {pendingMigrationsCount} migrations successfully.", pendingMigrations.Count());
            }
            else
            {
                Logger.LogInformation("No pending migrations to be executed.");
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
