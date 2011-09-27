using System.Data;
using Migrator.Framework;

namespace NuGetGallery {
    [Migration(20110804120000)]
    public class CreateUserTableMigration : Migration {
        // TODO: write PowerShell script to generate migration files (and have it generate the version stamp)

        public override void Up() {
            Database.AddTable("Users",
                new Column("[Key]", DbType.Int32, ColumnProperty.PrimaryKey | ColumnProperty.Identity | ColumnProperty.NotNull),
                new Column("Username", DbType.String, ColumnProperty.NotNull | ColumnProperty.Unique),
                new Column("HashedPassword", DbType.String, ColumnProperty.NotNull),
                new Column("ApiKey", DbType.Guid, 16, ColumnProperty.NotNull | ColumnProperty.Unique, "newid()"),
                new Column("EmailAddress", DbType.String, ColumnProperty.Null),
                new Column("UnconfirmedEmailAddress", DbType.String, ColumnProperty.Null),
                new Column("EmailAllowed", DbType.Boolean, 1, ColumnProperty.NotNull, true),
                new Column("EmailConfirmationToken", DbType.String, 32, ColumnProperty.Null),
                new Column("PasswordResetToken", DbType.String, 32, ColumnProperty.Null),
                new Column("PasswordResetTokenExpirationDate", DbType.DateTime, ColumnProperty.Null)
            );

            Database.ExecuteNonQuery(@"CREATE UNIQUE NONCLUSTERED INDEX Users_EmailAddress
                ON [Users] (EmailAddress)
                WHERE EmailAddress IS NOT NULL");
        }

        public override void Down() {
            Database.ExecuteNonQuery(@"DROP INDEX Users.Users_EmailAddress");
            Database.RemoveTable("Users");
        }
    }
}