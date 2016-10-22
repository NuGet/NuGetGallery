namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class MaxPackageId100 : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.PackageRegistrations", "Id", c => c.String(nullable: false, maxLength: 100));
            AlterColumn("dbo.PackageDependencies", "Id", c => c.String(maxLength: 100));
            AlterColumn("dbo.PackageLicenses", "Name", c => c.String(nullable: false, maxLength: 100));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.PackageLicenses", "Name", c => c.String(nullable: false, maxLength: 128));
            AlterColumn("dbo.PackageDependencies", "Id", c => c.String(maxLength: 128));
            AlterColumn("dbo.PackageRegistrations", "Id", c => c.String(nullable: false, maxLength: 128));
        }
    }
}
