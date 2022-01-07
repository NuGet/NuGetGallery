// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Migrations.Design;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGetGallery.DatabaseMigrationTools;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.DatabaseMigration.Facts
{
    public class PendingMigrationsFacts : IAsyncLifetime
    {
        private string _dbName;
        private readonly ITestOutputHelper _output;

        public PendingMigrationsFacts(ITestOutputHelper output)
        {
            _output = output;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            if (_dbName == null)
            {
                return;
            }

            const string connectionString = @"Data Source=(localdb)\mssqllocaldb; Initial Catalog=master; Integrated Security=True; MultipleActiveResultSets=True";
            using (var sqlConnection = new SqlConnection(connectionString))
            {
                await sqlConnection.OpenAsync();
                using (var sqlCommand = sqlConnection.CreateCommand())
                {
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
                    sqlCommand.CommandText = $"ALTER DATABASE {_dbName} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {_dbName};";
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
                    await sqlCommand.ExecuteNonQueryAsync();
                }
            }
        }

        public static IEnumerable<object[]> TestData
        {
            get
            {
                var factory = new MigrationContextFactory();

                var currentTimestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssFFFFFFF");

                var galleryDbName = $"PendingMigrationsTest{currentTimestamp}Gallery";
                var gallerySqlConnectionFactory = new Mock<ISqlConnectionFactory<GalleryDbConfiguration>>();
                gallerySqlConnectionFactory
                    .Setup(x => x.CreateAsync())
                    .ReturnsAsync(() => new SqlConnection(
                        $@"Data Source=(localdb)\mssqllocaldb; Initial Catalog={galleryDbName}; Integrated Security=True; MultipleActiveResultSets=True"));

                var supportRequestsDbName = $"PendingMigrationsTest{currentTimestamp}SupportRequests";
                var supportRequestsSqlConnectionFactory = new Mock<ISqlConnectionFactory<SupportRequestDbConfiguration>>();
                supportRequestsSqlConnectionFactory
                    .Setup(x => x.CreateAsync())
                    .ReturnsAsync(() => new SqlConnection(
                        $@"Data Source=(localdb)\mssqllocaldb; Initial Catalog={supportRequestsDbName}; Integrated Security=True; MultipleActiveResultSets=True"));

                var serviceProvider = new Mock<IServiceProvider>();

                serviceProvider
                    .Setup(x => x.GetService(typeof(ISqlConnectionFactory<GalleryDbConfiguration>)))
                    .Returns(() => gallerySqlConnectionFactory.Object);

                serviceProvider
                    .Setup(x => x.GetService(typeof(ISqlConnectionFactory<SupportRequestDbConfiguration>)))
                    .Returns(() => supportRequestsSqlConnectionFactory.Object);

                yield return new object[] { galleryDbName, MigrationTargetDatabaseArgumentNames.GalleryDatabase, factory, serviceProvider.Object };
                yield return new object[] { supportRequestsDbName, MigrationTargetDatabaseArgumentNames.SupportRequestDatabase, factory, serviceProvider.Object };

                // Validation DB is not tested here because:
                // 1) The migrations do no live in this repository so it's a bit late if we find out there are pending changes at this point.
                // 2) There is a type load exception since a type of name EntitiesConfiguration is already loaded by Gallery DB or Suppport Requests DB.
            }
        }

        [SkipTestForUnsignedBuildsTheory]
        [MemberData(nameof(TestData))]
        public async Task NoPendingMigrations(string dbName, string argumentName, MigrationContextFactory factory, IServiceProvider serviceProvider)
        {
            _dbName = dbName;

            var migrationContext = await factory.CreateMigrationContextAsync(argumentName, serviceProvider);

            var dbMigrator = migrationContext.GetDbMigrator();
            var migrations = dbMigrator.GetLocalMigrations();
            dbMigrator.Update(migrations.Last());

            var migrationScaffolder = new MigrationScaffolder(dbMigrator.Configuration);

            var migrationName = $"TestMigration{DateTimeOffset.UtcNow:yyyyMMddHHmmssFFFFFFF}";
            var result = migrationScaffolder.Scaffold(migrationName);

            _output.WriteLine("Migration content:");
            _output.WriteLine(new string('-', 60));
            _output.WriteLine(result.UserCode);
            _output.WriteLine(new string('-', 60));

            Assert.Equal(
                $@"namespace {dbMigrator.Configuration.MigrationsNamespace}
{{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class {migrationName} : DbMigration
    {{
        public override void Up()
        {{
        }}
        
        public override void Down()
        {{
        }}
    }}
}}
", result.UserCode);
        }
    }
}
