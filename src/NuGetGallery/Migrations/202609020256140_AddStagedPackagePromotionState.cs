namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddStagedPackagePromotionState : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.StagedPackages", "ActivePromotionId", c => c.Guid());
        }
        
        public override void Down()
        {
            DropColumn("dbo.StagedPackages", "ActivePromotionId");
        }
    }
}
