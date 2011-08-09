using System.Data;
using Migrator.Framework;

namespace NuGetGallery
{
    [Migration(20110804120000)]
    public class CreateUserTableMigration : Migration
    {
        // TODO: write PowerShell script to generate migration files (and have it generate the version stamp)
        
        public override void Up()
        {
            Database.AddTable("Users",
                new Column("[Key]", DbType.Int32, ColumnProperty.PrimaryKey | ColumnProperty.Identity | ColumnProperty.NotNull),
                new Column("Username", DbType.String, ColumnProperty.NotNull | ColumnProperty.Unique),
                new Column("HashedPassword", DbType.String, ColumnProperty.NotNull),
                new Column("EmailAddress", DbType.String, ColumnProperty.NotNull | ColumnProperty.Unique));
        }

        public override void Down()
        {
            Database.RemoveTable("Users");
        }
    }
}