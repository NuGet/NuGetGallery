namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddStagedSymbolPromotionControls : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.StagedSymbolPackages", "PromotionMessageSentDate", c => c.DateTime());
            AddColumn("dbo.StagedSymbolPackages", "MutationRevision", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.StagedSymbolPackages", "MutationRevision");
            DropColumn("dbo.StagedSymbolPackages", "PromotionMessageSentDate");
        }
    }
}
