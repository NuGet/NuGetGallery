using System.Data;
using Migrator.Framework;

namespace NuGetGallery.Data.Migrations {
    [Migration(20110804140000)]
    public class CreatePackageRegistrationsTableMigration : Migration {
        public override void Up() {
            Database.AddTable("PackageRegistrations",
                new Column("[Key]", DbType.Int32, ColumnProperty.PrimaryKey | ColumnProperty.Identity | ColumnProperty.NotNull),
                new Column("Id", DbType.String, ColumnProperty.NotNull | ColumnProperty.Unique),
                new Column("DownloadCount", DbType.Int32, ColumnProperty.NotNull, 0));
        }

        public override void Down() {
            Database.RemoveTable("PackageRegistrations");
        }
    }
}