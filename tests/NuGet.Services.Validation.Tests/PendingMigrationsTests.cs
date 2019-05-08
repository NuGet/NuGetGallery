// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using System.Data.Entity.Migrations.Design;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.Validation.Tests
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
                    sqlCommand.CommandText = $"ALTER DATABASE {_dbName} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {_dbName};";
                    await sqlCommand.ExecuteNonQueryAsync();
                }
            }
        }

        [Fact]
        public void NoPendingMigrations()
        {
            var currentTimestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssFFFFFFF");
            _dbName = $"PendingMigrationsTest{currentTimestamp}Validation";
            var connectionString = $@"Data Source=(localdb)\mssqllocaldb; Initial Catalog={_dbName}; Integrated Security=True; MultipleActiveResultSets=True";

            var migrationsConfiguration = new ValidationMigrationsConfiguration
            {
                TargetDatabase = new DbConnectionInfo(connectionString, "System.Data.SqlClient"),
            };

            var dbMigrator = new DbMigrator(migrationsConfiguration);
            var migrations = dbMigrator.GetLocalMigrations();
            dbMigrator.Update(migrations.Last());

            var migrationScaffolder = new MigrationScaffolder(dbMigrator.Configuration);

            var migrationName = $"TestMigration{currentTimestamp}";
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
