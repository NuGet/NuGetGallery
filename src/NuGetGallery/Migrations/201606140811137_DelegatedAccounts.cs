namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class DelegatedAccounts : DbMigration
    {
        public override void Up()
        {
            // Drop API key for "microsoft" user
            Sql(@"DELETE FROM [dbo].[Credentials]
                  WHERE [UserKey] = (SELECT [Key] FROM [dbo].[Users] WHERE [Username] = 'microsoft')
                    AND [Type] = 'apikey.v1'");
        }
        
        public override void Down()
        {
        }
    }
}
