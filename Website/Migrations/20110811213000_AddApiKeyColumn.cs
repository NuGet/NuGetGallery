using System.Data;
using Migrator.Framework;

namespace NuGetGallery.Data.Migrations {
    [Migration(20110811210300)]
    public class AddApiKeyColumnMigration : Migration {
        public override void Up() {
            Database.AddColumn("Users", "ApiKey", DbType.Guid, 16, ColumnProperty.NotNull, "newid()");
            Database.AddUniqueConstraint("UQ_Users_ApiKey", "Users", "ApiKey");
        }

        public override void Down() {
            // The Down failed in the past because I didn't specify the unique index's name, and so couldn't easily remove it. 
            // If you have a problem running this Down(), you'll want to manually remove the unique index.
            Database.RemoveConstraint("Users", "UQ_Users_ApiKey");
            Database.RemoveColumn("Users", "ApiKey");
        }
    }
}