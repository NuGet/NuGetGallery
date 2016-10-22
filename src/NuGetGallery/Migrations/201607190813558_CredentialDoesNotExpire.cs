namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class CredentialDoesNotExpire : DbMigration
    {
        public override void Up()
        {
            // Remove expiration dates on API keys
            Sql("UPDATE [dbo].[Credentials] SET [Expires] = NULL WHERE [Type] = 'apikey.v1'");
        }
        
        public override void Down()
        {
        }
    }
}
