namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddPackageOwnershipRequestsPage : DbMigration
    {
        public override void Up()
        {
            CreateIndex("dbo.PackageOwnerRequests", "PackageRegistrationKey");
            AddForeignKey("dbo.PackageOwnerRequests", "PackageRegistrationKey", "dbo.PackageRegistrations", "Key", cascadeDelete: true);
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.PackageOwnerRequests", "PackageRegistrationKey", "dbo.PackageRegistrations");
            DropIndex("dbo.PackageOwnerRequests", new[] { "PackageRegistrationKey" });
        }
    }
}
