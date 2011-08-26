using System.Data;
using Migrator.Framework;

namespace NuGetGallery.Data.Migrations {
    [Migration(20110825225500)]
    public class CreateRolesTable : Migration {
        public override void Up() {
            Database.AddTable("Roles",
                new Column("[Key]", DbType.Int32, ColumnProperty.PrimaryKey | ColumnProperty.Identity | ColumnProperty.NotNull),
                new Column("Name", DbType.String, ColumnProperty.NotNull));

            Database.ExecuteNonQuery("INSERT Roles SELECT 'Administrators'");
        }

        public override void Down() {
            Database.RemoveTable("Roles");
        }
    }
}