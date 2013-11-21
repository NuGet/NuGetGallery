namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RemoveOldCredentialColumns : DbMigration
    {
        public override void Up()
        {
            // Columns must be dropped manually after verifying data is transferred
            //DropColumn("dbo.Users", "ApiKey");
            //DropColumn("dbo.Users", "HashedPassword");
            //DropColumn("dbo.Users", "PasswordHashAlgorithm");

            Sql(@"
                IF EXISTS (SELECT * FROM sys.views WHERE name = 'UsersAndCredentials')
                    DROP VIEW [dbo].[UsersAndCredentials]");
            Sql(@"
                CREATE VIEW UsersAndCredentials AS
                    SELECT u.Username, c.[Type], c.Value
                    FROM Users u
                    LEFT OUTER JOIN [Credentials] c ON c.UserKey = u.[Key]");
        }
        
        public override void Down()
        {
            // Columns must be added manually
            //AddColumn("dbo.Users", "PasswordHashAlgorithm", c => c.String());
            //AddColumn("dbo.Users", "HashedPassword", c => c.String(maxLength: 256));
            //AddColumn("dbo.Users", "ApiKey", c => c.Guid(nullable: false));
        }
    }
}
