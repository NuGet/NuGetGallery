using System.Data;
using Migrator.Framework;

namespace NuGetGallery.Data.Migrations {
    [Migration(20110808110000)]
    public class CreatePackageDependenciesTableMigration : Migration {
        public override void Up() {
            Database.AddTable("PackageDependencies",
                new Column("[Key]", DbType.Int32, ColumnProperty.PrimaryKey | ColumnProperty.Identity | ColumnProperty.NotNull),
                new Column("PackageKey", DbType.Int32, ColumnProperty.NotNull),
                new Column("Id", DbType.String, ColumnProperty.NotNull),
                new Column("VersionRange", DbType.String, ColumnProperty.NotNull));

            Database.AddForeignKey("FK_PackageDependencies_Packages", "PackageDependencies", "PackageKey", "Packages", "[Key]");
        }

        public override void Down() {
            Database.RemoveTable("PackageDependencies");
        }
    }
}