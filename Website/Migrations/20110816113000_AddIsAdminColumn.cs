using System.Data;
using Migrator.Framework;

namespace NuGetGallery.Data.Migrations {
    [Migration(20110816113000)]
    public class AddIsAdminColumnMigration : Migration {
        public override void Up() {
            Database.AddColumn("Users", "IsAdmin", DbType.Boolean, false);
        }

        public override void Down() {
            Database.RemoveColumn("Users", "IsAdmin");
        }
    }
}