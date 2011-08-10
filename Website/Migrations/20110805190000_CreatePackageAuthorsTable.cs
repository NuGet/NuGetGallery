using System.Data;
using Migrator.Framework;

namespace NuGetGallery.Data.Migrations {
    [Migration(20110805190000)]
    public class CreatePackageAuthorsTableMigration : Migration {
        public override void Up() {
            Database.AddTable("PackageAuthors",
                new Column("[Key]", DbType.Int32, ColumnProperty.PrimaryKey | ColumnProperty.Identity | ColumnProperty.NotNull),
                new Column("PackageKey", DbType.Int32, ColumnProperty.NotNull),
                new Column("Name", DbType.String, ColumnProperty.NotNull));

            Database.AddForeignKey("FK_PackageAuthors_Packages", "PackageAuthors", "PackageKey", "Packages", "[Key]");
        }

        public override void Down() {
            Database.RemoveTable("PackageAuthors");
        }
    }
}