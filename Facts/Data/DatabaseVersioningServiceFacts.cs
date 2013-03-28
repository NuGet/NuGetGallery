using System;
using System.Collections.Generic;
using System.Data.Entity.Migrations;
using System.Data.Entity.Migrations.Infrastructure;
using System.Linq.Expressions;
using Moq;
using NuGetGallery.Data.Migrations;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery.Data
{
    public class DatabaseVersioningServiceFacts
    {
        public class TheGetVersionMethod
        {
            [Fact]
            public void GivenANullOrEmptyId_ItThrowsArgumentException()
            {
                ContractAssert.ThrowsArgNullOrEmpty(s => new TestableDatabaseVersioningService().GetVersion(s), "id");
            }

            [Theory]
            [InlineData(" ")]
            [InlineData("NotAMigration")]
            [InlineData("234908234")]
            [InlineData("Foo_1238")]
            [InlineData("12345678901234_NotQuiteEnoughNumbers")]
            [InlineData("1234567890123456_TooManyNumbers")]
            public void GivenAnInvalidMigrationIdString_ItThrowsArgumentException(string migrationId)
            {
                ContractAssert.ThrowsArgException(
                    () => new TestableDatabaseVersioningService().GetVersion(migrationId), 
                    "id",
                    String.Format(Strings.InvalidEntityFrameworkMigrationId, migrationId));
            }

            [Theory]
            [InlineData("201302282115583_NotARealMigrationType", "NotARealMigrationType", "2013-02-28 21:15:58.3", "")]
            [InlineData("201302072118537_MovingGallerySettingsToConfiguration", "MovingGallerySettingsToConfiguration", "2013-02-07 21:18:53.7", "")]
            [InlineData("201302282115583_AddMinRequiredVersionColumn", "AddMinRequiredVersionColumn", "2013-02-28 21:15:58.3", "Adds the Minimum Required Version Column to the Packages table")]
            public void GivenAValidMigrationIdString_ItCorrectlyParsesTheString(string migrationId, string name, string date, string description)
            {
                // Act
                var ver = new TestableDatabaseVersioningService().GetVersion(migrationId);

                // Assert
                Assert.Equal(migrationId, ver.Id);
                Assert.Equal(name, ver.Name);
                Assert.Equal(DateTime.Parse(date), ver.CreatedUtc);
                Assert.Equal(description, ver.Description);
            }
        }

        public class TheUpdateToLatestMethod
        {
            [Fact]
            public void ItUpdatesTheMigratorAndReloadsMigrations()
            {
                const string expectedMigration = "NewMigration";

                // Arrange
                var service = new TestableDatabaseVersioningService();
                service.MockMigrator.Setup(m => m.Update()).Callback(() =>
                {
                    // Now that we've been called, "add" a migration to test that they get reloaded
                    var newMigrations = new[] { expectedMigration };
                    service.MockMigrator.Setup(m => m.GetDatabaseMigrations()).Returns(newMigrations); 
                    service.MockMigrator.Setup(m => m.GetLocalMigrations()).Returns(newMigrations);
                    service.MockMigrator.Setup(m => m.GetPendingMigrations()).Returns(newMigrations);
                });

                // Assume
                Assert.False(service.AppliedVersions.Contains(expectedMigration));
                Assert.False(service.AvailableVersions.Contains(expectedMigration));
                Assert.False(service.PendingVersions.Contains(expectedMigration));

                // Act
                service.UpdateToLatest();

                // Assert
                service.MockMigrator.Verify(m => m.Update());
                Assert.True(service.AppliedVersions.Contains(expectedMigration));
                Assert.True(service.AvailableVersions.Contains(expectedMigration));
                Assert.True(service.PendingVersions.Contains(expectedMigration));
            }
        }

        public class TheUpdateToMinimumMethod
        {
            [Fact]
            public void WhenTheMigrationHasAlreadyBeenApplied_ItDoesNothing()
            {
                // Arrange
                var service = new TestableDatabaseVersioningService();
                service.MockMigrator.Setup(m => m.GetDatabaseMigrations())
                    .Returns(new[] {DatabaseVersioningService.MinimumMigrationId});

                // Act
                service.UpdateToMinimum();

                // Assert
                service.MockMigrator.Verify(m => m.Update(), Times.Never());
                service.MockMigrator.Verify(m => m.Update(DatabaseVersioningService.MinimumMigrationId), Times.Never());
            }

            [Fact]
            public void WhenTheMigrationHasNotAlreadyBeenApplied_ItMigratesAndReloads()
            {
                const string expectedMigration = "NewMigration";

                // Arrange
                var service = new TestableDatabaseVersioningService();
                service.MockMigrator.Setup(m => m.Update(DatabaseVersioningService.MinimumMigrationId)).Callback(() =>
                {
                    // Now that we've been called, "add" a migration to test that they get reloaded
                    var newMigrations = new[] { expectedMigration };
                    service.MockMigrator.Setup(m => m.GetDatabaseMigrations()).Returns(newMigrations);
                    service.MockMigrator.Setup(m => m.GetLocalMigrations()).Returns(newMigrations);
                    service.MockMigrator.Setup(m => m.GetPendingMigrations()).Returns(newMigrations);
                });

                // Assume
                Assert.False(service.AppliedVersions.Contains(expectedMigration));
                Assert.False(service.AvailableVersions.Contains(expectedMigration));
                Assert.False(service.PendingVersions.Contains(expectedMigration));

                // Act
                service.UpdateToMinimum();

                // Assert
                service.MockMigrator.Verify(m => m.Update(DatabaseVersioningService.MinimumMigrationId));
                Assert.True(service.AppliedVersions.Contains(expectedMigration));
                Assert.True(service.AvailableVersions.Contains(expectedMigration));
                Assert.True(service.PendingVersions.Contains(expectedMigration));
            }
        }

        public class TheNNNVersionProperties
        {
            [Fact]
            public void ReturnTheValueFromTheMigratorAtTimeOfFirstAccess()
            {
                ReturnTheValueFromTheMigratorAtTimeOfFirstAccessTheory(s => s.AppliedVersions, m => m.GetDatabaseMigrations());
                ReturnTheValueFromTheMigratorAtTimeOfFirstAccessTheory(s => s.AvailableVersions, m => m.GetLocalMigrations());
                ReturnTheValueFromTheMigratorAtTimeOfFirstAccessTheory(s => s.PendingVersions, m => m.GetPendingMigrations());
            }

            private void ReturnTheValueFromTheMigratorAtTimeOfFirstAccessTheory(
                Func<DatabaseVersioningService, ISet<string>> property,
                Expression<Func<IDbMigrator, IEnumerable<string>>> migratorMethod)
            {
                const string expectedMigration = "Foo";
                const string unexpectedMigration = "Bar";

                // Arrange
                var service = new TestableDatabaseVersioningService();
                service.MockMigrator.Setup(migratorMethod).Returns(new[] {expectedMigration});

                // Act
                Assert.True(property(service).Contains(expectedMigration));
                service.MockMigrator.Setup(migratorMethod).Returns(new[] { expectedMigration }); // Make clear that the property has to be reloaded.
                Assert.False(property(service).Contains(unexpectedMigration));
            }
        }

        public class TestableDatabaseVersioningService : DatabaseVersioningService
        {
            public Mock<IDbMigrator> MockMigrator { get; private set; }

            public TestableDatabaseVersioningService()
            {
                Migrator = (MockMigrator = new Mock<IDbMigrator>()).Object;
            }
        }
    }
}
