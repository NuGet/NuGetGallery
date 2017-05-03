namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddSemVer2LatestVersionColumns : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "IsLatestSemVer2", c => c.Boolean(nullable: false));
            AddColumn("dbo.Packages", "IsLatestStableSemVer2", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Packages", "IsLatestStableSemVer2");
            DropColumn("dbo.Packages", "IsLatestSemVer2");
        }
    }
}
