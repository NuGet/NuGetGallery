namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddStagingPromotionMessageSentDates : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.StagedPackages", "PromotionMessageSentDate", c => c.DateTime());
            AddColumn("dbo.StagingGroups", "PromotionMessageSentDate", c => c.DateTime());
        }
        
        public override void Down()
        {
            DropColumn("dbo.StagingGroups", "PromotionMessageSentDate");
            DropColumn("dbo.StagedPackages", "PromotionMessageSentDate");
        }
    }
}
