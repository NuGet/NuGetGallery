// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Migrations.Design;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Services.Entities;
using NuGet.Services.Staging;
using NuGetGallery;
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

        [Fact]
        public async Task CanApplyAndRollbackPackageStaging()
        {
            _dbName = $"PackageStagingMigrationTest{Guid.NewGuid():N}";
            var connectionString = $@"Data Source=(localdb)\mssqllocaldb; Initial Catalog={_dbName}; Integrated Security=True; MultipleActiveResultSets=True";
            const string baselineMigration = "202608031928454_AddFederatedCredentialPolicyScopes";

            using (var migrationConnection = new SqlConnection(connectionString))
            {
                var migrationContext = new GalleryDbMigrationContext(migrationConnection);
                var dbMigrator = migrationContext.GetDbMigrator();
                dbMigrator.Update(baselineMigration);

                using (var sqlConnection = new SqlConnection(connectionString))
                {
                    await sqlConnection.OpenAsync();
                    using (var command = sqlConnection.CreateCommand())
                    {
                        command.CommandText = @"INSERT dbo.Users (Username, EmailAllowed, IsDeleted)
                            VALUES (N'StagingActive', 1, 0), (N'StagingDeleted', 0, 1)";
                        await command.ExecuteNonQueryAsync();

                        dbMigrator.Update("AddPackageStaging");

                        command.CommandText = @"SELECT COUNT(*) FROM sys.columns AS c
                            INNER JOIN sys.tables AS t ON t.object_id = c.object_id
                            WHERE t.name IN (N'StagedPackages', N'StagedSymbolPackages', N'StagingGroups')
                              AND c.name = N'PromotionAcceptedAt'
                              AND TYPE_NAME(c.user_type_id) = N'datetime2'
                              AND c.scale = 7 AND c.is_nullable = 1";
                        Assert.Equal(3, await command.ExecuteScalarAsync());

                        command.CommandText = @"SELECT COUNT(*) FROM dbo.Users
                            WHERE (Username = N'StagingActive' AND NotifyPackageStaged = 1)
                               OR (Username = N'StagingDeleted' AND NotifyPackageStaged = 0)";
                        Assert.Equal(2, await command.ExecuteScalarAsync());

                        command.CommandText = @"INSERT dbo.Users (Username, EmailAllowed)
                            VALUES (N'StagingNew', 1);
                            SELECT NotifyPackageStaged FROM dbo.Users WHERE Username = N'StagingNew'";
                        Assert.Equal(true, await command.ExecuteScalarAsync());

                        command.CommandText = @"INSERT dbo.StagingGroups (OwnerKey, Id, Name, CreatedDate)
                            SELECT [Key], N'migration-test', N'Migration test', SYSUTCDATETIME()
                            FROM dbo.Users WHERE Username = N'StagingActive';
                            SELECT COUNT(*) FROM dbo.StagingGroups
                            WHERE MutationRevision = 0
                              AND ExpirationDate BETWEEN DATEADD(day, 30, DATEADD(minute, -1, SYSUTCDATETIME()))
                                                     AND DATEADD(day, 30, DATEADD(minute, 1, SYSUTCDATETIME()))";
                        Assert.Equal(1, await command.ExecuteScalarAsync());

                        dbMigrator.Update(baselineMigration);

                        command.CommandText = @"SELECT COUNT(*) FROM sys.tables
                            WHERE name IN (N'StagedPackageIdentities', N'StagedPackages', N'StagedSymbolPackages',
                                           N'StagingGroups', N'StagingBlobCleanups')";
                        Assert.Equal(0, await command.ExecuteScalarAsync());

                        command.CommandText = "SELECT COL_LENGTH(N'dbo.Users', N'NotifyPackageStaged')";
                        Assert.Equal(DBNull.Value, await command.ExecuteScalarAsync());

                        command.CommandText = "SELECT COUNT(*) FROM dbo.Users WHERE Username LIKE N'Staging%'";
                        Assert.Equal(3, await command.ExecuteScalarAsync());

                        dbMigrator.Update("AddPackageStaging");

                        command.CommandText = @"SELECT COUNT(*) FROM dbo.Users
                            WHERE (Username IN (N'StagingActive', N'StagingNew') AND NotifyPackageStaged = 1)
                               OR (Username = N'StagingDeleted' AND NotifyPackageStaged = 0)";
                        Assert.Equal(3, await command.ExecuteScalarAsync());
                    }
                }
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public async Task GroupAcceptanceCommitsAllBatchesOrRollsBackOnALateConflict(int conflictTarget)
        {
            _dbName = $"GroupAcceptanceTest{Guid.NewGuid():N}";
            var connectionString = $@"Data Source=(localdb)\mssqllocaldb; Initial Catalog={_dbName}; Integrated Security=True; MultipleActiveResultSets=True";
            using (var migrationConnection = new SqlConnection(connectionString))
            {
                new GalleryDbMigrationContext(migrationConnection).GetDbMigrator().Update("AddPackageStaging");
            }

            using (var seed = new EntitiesContext(connectionString, readOnly: false))
            {
                var owner = new User("batch-owner") { EmailAddress = "owner@example.test" };
                var group = new StagingGroup { Owner = owner, Id = "batch-group", Name = "Batch group", CreatedDate = DateTime.UtcNow };
                var parents = new List<StagedPackage>();
                var symbols = new List<StagedSymbolPackage>();
                for (var index = 0; index < 101; index++)
                {
                    var registration = new PackageRegistration { Id = $"Batch.Package{index}" };
                    registration.Owners.Add(owner);
                    var package = new Package
                    {
                        PackageRegistration = registration,
                        Version = "1.0.0",
                        NormalizedVersion = "1.0.0",
                        Hash = "hash",
                        PackageStatusKey = PackageStatus.Staged,
                    };
                    var identity = new StagedPackageIdentity { Package = package, Owner = owner, StagingGroup = group };
                    parents.Add(new StagedPackage
                    {
                        StagedPackageIdentity = identity,
                        UploadedBlobPath = $"packages/{index}",
                        UploadedBlobETag = "etag",
                        UploadHash = "hash",
                        UploadedDate = DateTime.UtcNow,
                        Status = StagedPackageStatus.Ready,
                    });
                    symbols.Add(new StagedSymbolPackage
                    {
                        StagedPackageIdentity = identity,
                        SymbolPackage = new SymbolPackage { Package = package, Hash = "hash", Created = DateTime.UtcNow, StatusKey = PackageStatus.Staged },
                        UploadedBlobPath = $"symbols/{index}",
                        UploadedBlobETag = "etag",
                        UploadedDate = DateTime.UtcNow,
                        PromotionMessageSentDate = DateTime.UtcNow.AddDays(-1),
                        Status = StagedPackageStatus.Ready,
                    });
                }

                seed.StagedPackages.AddRange(parents);
                seed.StagedSymbolPackages.AddRange(symbols);
                await seed.SaveChangesAsync();
                for (var index = 0; index < parents.Count; index++)
                {
                    var identity = parents[index].StagedPackageIdentity;
                    identity.CurrentStagedPackageKey = parents[index].Key;
                    identity.CurrentStagedSymbolPackageKey = symbols[index].Key;
                }

                await seed.SaveChangesAsync();
            }

            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            enqueuer.Setup(service => service.SendMessageAsync(It.IsAny<StagingPromotionMessage>())).Returns(Task.CompletedTask);
            var telemetry = new Mock<ITelemetryService>();
            using (var context = new EntitiesContext(connectionString, readOnly: false))
            using (var competingConnection = new SqlConnection(connectionString))
            {
                await competingConnection.OpenAsync();
                var group = context.StagingGroups.Include(candidate => candidate.Owner).Single();
                var authorization = new Mock<IPackageStagingAuthorizationService>();
                authorization.Setup(service => service.GetEnabledOwners(group.Owner)).Returns(new[] { group.Owner });
                var parentsChecked = 0;
                var symbolsChecked = 0;
                Action<int, bool> changeAttempt = (key, isSymbols) =>
                {
                    using (var command = competingConnection.CreateCommand())
                    {
                        if (isSymbols)
                        {
                            command.CommandText = "UPDATE dbo.StagedSymbolPackages SET MutationRevision = MutationRevision + 1 WHERE [Key] = @key";
                        }
                        else
                        {
                            command.CommandText = "UPDATE dbo.StagedPackages SET MutationRevision = MutationRevision + 1 WHERE [Key] = @key";
                        }

                        command.Parameters.AddWithValue("@key", key);
                        Assert.Equal(1, command.ExecuteNonQuery());
                    }
                };
                authorization.Setup(service => service.CanManage(group.Owner, It.IsAny<StagedPackage>()))
                    .Callback<User, StagedPackage>((user, attempt) =>
                    {
                        if (conflictTarget == 1 && ++parentsChecked == 101)
                        {
                            changeAttempt(attempt.Key, false);
                        }
                    }).Returns(true);
                authorization.Setup(service => service.CanManage(group.Owner, It.IsAny<StagedSymbolPackage>()))
                    .Callback<User, StagedSymbolPackage>((user, attempt) =>
                    {
                        if (conflictTarget == 2 && ++symbolsChecked == 101)
                        {
                            changeAttempt(attempt.Key, true);
                        }
                    }).Returns(true);
                var target = new PackageStagingPromotionService(authorization.Object, enqueuer.Object,
                    new EntityRepository<StagedPackage>(context), new EntityRepository<StagedSymbolPackage>(context), telemetry.Object, context);

                var result = await target.PromoteGroupAsync(group.Owner, group);

                Assert.Equal(conflictTarget == 0 ? StagingGroupPromotionResult.Accepted : StagingGroupPromotionResult.Conflict, result);
                Assert.Empty(context.ChangeTracker.Entries<StagedPackage>());
                Assert.Empty(context.ChangeTracker.Entries<StagedSymbolPackage>());
            }

            using (var verify = new EntitiesContext(connectionString, readOnly: false))
            {
                var group = await verify.StagingGroups.SingleAsync();
                var parents = await verify.StagedPackages.ToListAsync();
                var symbols = await verify.StagedSymbolPackages.ToListAsync();
                if (conflictTarget == 0)
                {
                    Assert.NotNull(group.ActivePromotionId);
                    Assert.NotNull(group.PromotionAcceptedAt);
                    Assert.All(parents, attempt =>
                    {
                        Assert.Equal(StagedPackageStatus.Promoting, attempt.Status);
                        Assert.Equal(group.ActivePromotionId, attempt.ActivePromotionId);
                        Assert.Equal(group.PromotionAcceptedAt, attempt.PromotionAcceptedAt);
                    });
                    Assert.All(symbols, attempt =>
                    {
                        Assert.Equal(StagedPackageStatus.Promoting, attempt.Status);
                        Assert.Equal(group.ActivePromotionId, attempt.ActivePromotionId);
                        Assert.Equal(group.PromotionAcceptedAt, attempt.PromotionAcceptedAt);
                        Assert.Null(attempt.PromotionMessageSentDate);
                    });
                    enqueuer.Verify(service => service.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Once);
                    telemetry.Verify(service => service.TrackStagingPromotionAccepted(group.ActivePromotionId.Value, It.IsAny<Package>(), It.IsAny<bool>()), Times.Exactly(202));
                }
                else
                {
                    Assert.Null(group.ActivePromotionId);
                    Assert.Null(group.PromotionAcceptedAt);
                    Assert.Null(group.PromotionMessageSentDate);
                    Assert.All(parents, attempt =>
                    {
                        Assert.Equal(StagedPackageStatus.Ready, attempt.Status);
                        Assert.Null(attempt.ActivePromotionId);
                        Assert.Null(attempt.PromotionAcceptedAt);
                    });
                    Assert.All(symbols, attempt =>
                    {
                        Assert.Equal(StagedPackageStatus.Ready, attempt.Status);
                        Assert.Null(attempt.ActivePromotionId);
                        Assert.Null(attempt.PromotionAcceptedAt);
                        Assert.NotNull(attempt.PromotionMessageSentDate);
                    });
                    enqueuer.VerifyNoOtherCalls();
                    telemetry.VerifyNoOtherCalls();
                }
            }
        }

        [Theory]
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
