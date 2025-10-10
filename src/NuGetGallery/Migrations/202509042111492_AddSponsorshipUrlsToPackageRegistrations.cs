namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddSponsorshipUrlsToPackageRegistrations : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.PackageRegistrations", "SponsorshipUrls", c => c.String(maxLength: 4000));
        }
        
        public override void Down()
        {
            DropColumn("dbo.PackageRegistrations", "SponsorshipUrls");
        }
    }
}
