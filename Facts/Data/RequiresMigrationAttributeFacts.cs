using System;
using System.Collections.Generic;
using System.Data.Entity.Migrations.Infrastructure;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Data.Migrations;
using Xunit;

namespace NuGetGallery.Data
{
    public class RequiresMigrationAttributeFacts
    {
        public class TheConstructor
        {
            [Fact]
            public void GivenANullMigrationType_ItThrowsArgumentNullException()
            {
                ContractAssert.ThrowsArgNull(() => new RequiresMigrationAttribute((Type)null), "migrationType");
            }

            [Fact]
            public void GivenANullOrEmptyMigrationId_ItThrowsArgumentException()
            {
                ContractAssert.ThrowsArgNullOrEmpty(s => new RequiresMigrationAttribute(s), "migrationId");
            }

            [Fact]
            public void GivenAMigrationId_ItSetsMigrationIdToThatValue()
            {
                Assert.Equal(
                    "abc123",
                    new RequiresMigrationAttribute("abc123").MigrationId);
            }

            [Fact]
            public void GivenAMigrationTypeThatIsNotActuallyAMigration_ItThrowsArgumentException()
            {
                ContractAssert.ThrowsArgException(
                    () => new RequiresMigrationAttribute(typeof(string)),
                    "migrationType",
                    String.Format(Strings.TypeIsNotAMigration, typeof(String).FullName));
            }

            [Fact]
            public void GivenAMigrationTypeThatIsAMigration_ItEventuallySetsMigrationIdToTheValueOfTheIdProperty()
            {
                Assert.Equal(
                    ((IMigrationMetadata)new Initial()).Id,
                    new RequiresMigrationAttribute(typeof(Initial)).MigrationId);
            }
        }
    }
}
