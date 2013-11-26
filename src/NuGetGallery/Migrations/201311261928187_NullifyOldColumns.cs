namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class NullifyOldColumns : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.Users", "ApiKey", c => c.Guid(nullable: true));
            AlterColumn("dbo.Users", "PasswordHashAlgorithm", c => c.String(nullable: true));
        }
        
        public override void Down()
        {
            // Do nothing on the down action.
        }
    }
}
