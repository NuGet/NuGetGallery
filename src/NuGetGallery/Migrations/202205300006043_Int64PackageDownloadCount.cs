namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Int64PackageDownloadCount : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.PackageRegistrations", "DownloadCount", c => c.Long(nullable: false));
            AlterColumn("dbo.Packages", "DownloadCount", c => c.Long(nullable: false));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.Packages", "DownloadCount", c => c.Int(nullable: false));
            AlterColumn("dbo.PackageRegistrations", "DownloadCount", c => c.Int(nullable: false));
        }
    }
}
