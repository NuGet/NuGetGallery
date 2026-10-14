namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    /// <summary>
    /// Persists the original acceptance time of active package, symbol, and group promotions.
    /// </summary>
    public partial class AddPromotionAcceptedAt : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.StagedPackages", "PromotionAcceptedAt", c => c.DateTime(precision: 7, storeType: "datetime2"));
            AddColumn("dbo.StagedSymbolPackages", "PromotionAcceptedAt", c => c.DateTime(precision: 7, storeType: "datetime2"));
            AddColumn("dbo.StagingGroups", "PromotionAcceptedAt", c => c.DateTime(precision: 7, storeType: "datetime2"));
        }
        
        public override void Down()
        {
            DropColumn("dbo.StagingGroups", "PromotionAcceptedAt");
            DropColumn("dbo.StagedSymbolPackages", "PromotionAcceptedAt");
            DropColumn("dbo.StagedPackages", "PromotionAcceptedAt");
        }
    }
}
