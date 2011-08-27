using System.Data;
using Migrator.Framework;

namespace NuGetGallery.Data.Migrations {
    [Migration(20110826215600)]
    public class AddEmailAllowedColumn : Migration {
        public override void Up() {
            Database.AddColumn("Users", "EmailAllowed", DbType.Boolean, 1, ColumnProperty.NotNull, true);
        }

        public override void Down() {
            Database.RemoveColumn("Users", "EmailAllowed");
        }
    }
}