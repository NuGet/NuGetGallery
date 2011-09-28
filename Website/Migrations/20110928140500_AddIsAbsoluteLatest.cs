using System.Data;
using Migrator.Framework;

namespace NuGetGallery.Migrations {
    [Migration(20110928140500)]
    public class AddIsAbsoluteLatest : Migration {
        public override void Down() {
            Database.AddColumn("Packages", "IsPrerelease", DbType.Boolean, defaultValue: false);
            Database.RemoveColumn("Packages", "IsAbsoluteLatest");
        }

        public override void Up() {
            Database.RemoveColumn("Packages", "IsPrerelease");
            Database.AddColumn("Packages", "IsAbsoluteLatest", DbType.Boolean, defaultValue: false);
        }
    }
}