namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddVulnerablePackageVersionRangeFirstPatchedVersion : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.VulnerablePackageVersionRanges", "FirstPatchedPackageVersion", c => c.String(maxLength: 64));
        }
        
        public override void Down()
        {
            DropColumn("dbo.VulnerablePackageVersionRanges", "FirstPatchedPackageVersion");
        }
    }
}
