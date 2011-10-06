using System;
using System.Data;
using Migrator.Framework;

namespace NuGetGallery.Migrations {
    [Migration(20111002190000)]
    public class PrereleasePackageChanges : Migration {
        public override void Up() {
            // This update is irreversible
            Database.Update("Packages", new[] { "Published" }, new[] { DateTime.UtcNow.ToString() }, where: "Published is null");
            Database.RemoveColumn("Packages", "IsAbsoluteLatest");
            Database.AddColumn("Packages", "IsPrerelease", DbType.Boolean, 1, ColumnProperty.NotNull, defaultValue: false);
            Database.AddColumn("Packages", "IsLatestStable", DbType.Boolean, 1, ColumnProperty.NotNull, defaultValue: false);
            Database.AddColumn("Packages", "Listed", DbType.Boolean, 1, ColumnProperty.NotNull, defaultValue: true);

            Database.ExecuteNonQuery("Update Packages set IsLatestStable = IsLatest");
            Database.ExecuteNonQuery("Update Packages set Listed = Case Unlisted When 0 then 1 else 0 end");
            Database.RemoveColumn("Packages", "Unlisted");
        }

        public override void Down() {
            Database.AddColumn("Packages", "IsAbsoluteLatest", DbType.Boolean, defaultValue: false);
            Database.RemoveColumn("Packages", "IsPrerelease");
            Database.RemoveColumn("Packages", "IsLatestStable");
            Database.AddColumn("Packages", "Unlisted", DbType.Boolean, ColumnProperty.NotNull);
            Database.ExecuteNonQuery("Update Packages set Unlisted = Case Listed When 0 then 1 else 0 end");
            Database.RemoveColumn("Packages", "Listed");
        }
    }
}