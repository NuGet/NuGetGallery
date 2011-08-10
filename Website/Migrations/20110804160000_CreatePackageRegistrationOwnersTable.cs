using System.Data;
using Migrator.Framework;

namespace NuGetGallery.Data.Migrations {
    [Migration(20110804160000)]
    public class CreatePackageRegistrationOwnersTableMigration : Migration {
        public override void Up() {
            Database.AddTable("PackageRegistrationOwners",
                new Column("PackageRegistrationKey", DbType.Int32, ColumnProperty.NotNull),
                new Column("UserKey", DbType.Int32, ColumnProperty.NotNull));

            Database.AddPrimaryKey(
                "PK_PackageOwners",
                "PackageRegistrationOwners",
                new[] { "PackageRegistrationKey", "UserKey" });

            Database.AddForeignKey("FK_PackageRegistrationOwners_PackageRegistrations", "PackageRegistrationOwners", "PackageRegistrationKey", "PackageRegistrations", "[Key]");
            Database.AddForeignKey("FK_PackageRegistrationOwners_Users", "PackageRegistrationOwners", "UserKey", "Users", "[Key]");
        }

        public override void Down() {
            Database.RemoveTable("PackageRegistrationOwners");
        }
    }
}