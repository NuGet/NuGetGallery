namespace NuGetGallery.Areas.Admin
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class DropPagerDutyColumn : DbMigration
    {
        public override void Up()
        {
            DropIndex("dbo.Admins", new[] { "PagerDutyUsername" });
            DropColumn("dbo.Admins", "PagerDutyUsername");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Admins", "PagerDutyUsername", c => c.String(nullable: false, maxLength: 255, unicode: false));
            CreateIndex("dbo.Admins", "PagerDutyUsername");
        }
    }
}
