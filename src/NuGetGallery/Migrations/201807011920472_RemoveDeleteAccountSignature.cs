namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RemoveDeleteAccountSignature : DbMigration
    {
        public override void Up()
        {
            DropColumn("dbo.AccountDeletes", "Signature");
        }
        
        public override void Down()
        {
            AddColumn("dbo.AccountDeletes", "Signature", c => c.String(nullable: false));
        }
    }
}
