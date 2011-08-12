using System.Data;
using Migrator.Framework;

namespace NuGetGallery.Data.Migrations {
    [Migration(20110811210300)]
    public class AddApiKeyColumnMigration : Migration {
        public override void Up() {
            Database.AddColumn("Users", "ApiKey", DbType.Guid, 16, ColumnProperty.NotNull | ColumnProperty.Unique, "newid()");
        }

        public override void Down() {
            Database.RemoveColumn("Users", "ApiKey");
        }
    }
}