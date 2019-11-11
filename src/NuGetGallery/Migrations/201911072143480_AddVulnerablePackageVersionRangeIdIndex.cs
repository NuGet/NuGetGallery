namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddVulnerablePackageVersionRangeIdIndex : DbMigration
    {
        public override void Up()
        {
            CreateIndex("dbo.VulnerablePackageVersionRanges", "PackageId");
        }
        
        public override void Down()
        {
            DropIndex("dbo.VulnerablePackageVersionRanges", new[] { "PackageId" });
        }
    }
}
