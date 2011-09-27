using System.Data;
using Migrator.Framework;

namespace NuGetGallery.Data.Migrations {
    [Migration(20110804150000)]
    public class CreatePackagesTableMigration : Migration {
        public override void Up() {
            // TODO: determine the right size for the string columns
            Database.AddTable("Packages",
                new Column("[Key]", DbType.Int32, ColumnProperty.PrimaryKey | ColumnProperty.Identity | ColumnProperty.NotNull),
                new Column("PackageRegistrationKey", DbType.Int32, ColumnProperty.NotNull),
                new Column("Copyright", DbType.String, 4000, ColumnProperty.Null),
                new Column("Created", DbType.DateTime, ColumnProperty.NotNull, "getdate()"),
                new Column("Description", DbType.String, 4000, ColumnProperty.NotNull),
                new Column("DownloadCount", DbType.Int32, ColumnProperty.Null),
                new Column("ExternalPackageUrl", DbType.String, 4000, ColumnProperty.Null),
                new Column("HashAlgorithm", DbType.String, 64, ColumnProperty.NotNull),
                new Column("Hash", DbType.String, 4000, ColumnProperty.NotNull),
                new Column("IconUrl", DbType.String, 4000, ColumnProperty.Null),
                new Column("IsLatest", DbType.Boolean, false),
                new Column("IsPrerelease", DbType.Boolean, false),
                new Column("LastUpdated", DbType.DateTime, ColumnProperty.NotNull, "getdate()"),
                new Column("LicenseUrl", DbType.String, 4000, ColumnProperty.Null),
                new Column("PackageFileSize", DbType.Int64, ColumnProperty.NotNull),
                new Column("ProjectUrl", DbType.String, 4000, ColumnProperty.Null),
                new Column("Published", DbType.DateTime, ColumnProperty.Null),
                new Column("RequiresLicenseAcceptance", DbType.Boolean, ColumnProperty.NotNull),
                new Column("Summary", DbType.String, 4000, ColumnProperty.Null),
                new Column("Tags", DbType.String, 4000, ColumnProperty.Null),
                new Column("Title", DbType.String, 4000, ColumnProperty.Null),
                new Column("Version", DbType.String, ColumnProperty.NotNull),
                new Column("Unlisted", DbType.Boolean, ColumnProperty.NotNull),
                new Column("FlattenedAuthors", DbType.String, ColumnProperty.NotNull),
                new Column("FlattenedDependencies", DbType.String, ColumnProperty.Null));

            Database.AddForeignKey("FK_Packages_PackageRegistrations", "Packages", "PackageRegistrationKey", "PackageRegistrations", "[Key]");
            Database.AddUniqueConstraint("UQ_Packages_KeyAndVersion", "Packages", new[] { "[Key]", "Version" });
        }

        public override void Down() {
            Database.RemoveForeignKey("Packages", "FK_Packages_PackageRegistrations");
            Database.RemoveTable("Packages");
        }
    }
}