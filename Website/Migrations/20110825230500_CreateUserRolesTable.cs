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

            Database.ExecuteNonQuery(@"INSERT UserRoles SELECT UserKey = u.[Key], RoleKey = r.[Key] 
FROM Users u INNER JOIN Roles r ON r.Name = 'Administrators' WHERE u.IsAdmin = 1");

            Database.RemoveColumn("Users", "IsAdmin");
        }

        public override void Down() {
            Database.AddColumn("Users", "IsAdmin", DbType.Boolean, false);
            Database.ExecuteNonQuery(@"UPDATE Users Set IsAdmin = 1 WHERE [Key] IN (
SELECT UserKey FROM UserRoles INNER JOIN Roles ON [Key] = RoleKey WHERE Name = 'Administrators'
)");
            Database.RemoveTable("UserRoles");

        }
    }
}