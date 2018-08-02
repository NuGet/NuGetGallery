namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AccountDeleteSignatureNotRequired : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.AccountDeletes", "Signature", c => c.String());
        }
        
        public override void Down()
        {
            AlterColumn("dbo.AccountDeletes", "Signature", c => c.String(nullable: false));
        }
    }
}
