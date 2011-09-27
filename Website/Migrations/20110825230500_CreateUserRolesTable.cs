using System.Data;
using Migrator.Framework;

namespace NuGetGallery.Data.Migrations {
    [Migration(20110825230500)]
    public class CreateUserRolesTable : Migration {
        public override void Up() {
            Database.AddTable("UserRoles",
                new Column("UserKey", DbType.Int32, ColumnProperty.NotNull),
                new Column("RoleKey", DbType.Int32, ColumnProperty.NotNull));

            Database.AddPrimaryKey(
                "PK_UserRoles",
                "UserRoles",
                new[] { "UserKey", "RoleKey" });

            Database.AddForeignKey("FK_UserRoles_Users", "UserRoles", "UserKey", "Users", "[Key]");
            Database.AddForeignKey("FK_UserRoles_Roles", "UserRoles", "RoleKey", "Roles", "[Key]");
        }

        public override void Down() {
            Database.RemoveTable("UserRoles");
        }
    }
}