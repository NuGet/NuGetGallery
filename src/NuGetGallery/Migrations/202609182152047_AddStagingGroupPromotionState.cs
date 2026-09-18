namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddStagingGroupPromotionState : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.StagingGroups", "ActivePromotionId", c => c.Guid());
            AddColumn("dbo.StagingGroups", "RowVersion", c => c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"));
        }
        
        public override void Down()
        {
            DropColumn("dbo.StagingGroups", "RowVersion");
            DropColumn("dbo.StagingGroups", "ActivePromotionId");
        }
    }
}
