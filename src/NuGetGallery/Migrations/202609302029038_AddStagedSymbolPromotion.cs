namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddStagedSymbolPromotion : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.StagedSymbolPackages", "ActivePromotionId", c => c.Guid());
        }
        
        public override void Down()
        {
            DropColumn("dbo.StagedSymbolPackages", "ActivePromotionId");
        }
    }
}
