using System.Data;
using Migrator.Framework;

namespace NuGetGallery.Data.Migrations {
    [Migration(20110809083000)]
    public class CreatePackageReviewsTableMigration : Migration {
        public override void Up() {
            Database.AddTable("PackageReviews",
                new Column("[Key]", DbType.Int32, ColumnProperty.PrimaryKey | ColumnProperty.Identity | ColumnProperty.NotNull),
                new Column("PackageKey", DbType.Int32, ColumnProperty.NotNull),
                new Column("Review", DbType.String, ColumnProperty.Null),
                new Column("Rating", DbType.Int32, ColumnProperty.NotNull));

            Database.AddForeignKey("FK_PackageReviews_Packages", "PackageReviews", "PackageKey", "Packages", "[Key]");
        }

        public override void Down() {
            Database.RemoveTable("PackageReviews");
        }
    }
}