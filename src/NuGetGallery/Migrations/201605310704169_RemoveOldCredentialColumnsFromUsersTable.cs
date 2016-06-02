using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    /// <summary>
    /// These were supposed to have run with <see cref="RemoveOldCredentialColumns"/>, which did not happen.
    /// </summary>
    public partial class RemoveOldCredentialColumnsFromUsersTable : DbMigration
    {
        public override void Up()
        {
            DropColumn("dbo.Users", "ApiKey");
            DropColumn("dbo.Users", "HashedPassword");
            DropColumn("dbo.Users", "PasswordHashAlgorithm");
        }

        public override void Down()
        {
            AddColumn("dbo.Users", "PasswordHashAlgorithm", c => c.String());
            AddColumn("dbo.Users", "HashedPassword", c => c.String(maxLength: 256));
            AddColumn("dbo.Users", "ApiKey", c => c.Guid(nullable: false));
        }
    }
}
