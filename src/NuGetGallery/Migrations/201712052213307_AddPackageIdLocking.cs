namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddPackageIdLocking : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.PackageRegistrations", "IsLocked", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.PackageRegistrations", "IsLocked");
        }
    }
}
