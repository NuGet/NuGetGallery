namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class IX_AddIsVerifiedDownloadCountIndexForPackageRegistrationsTable : DbMigration
    {
        public override void Up()
        {
            // Used for getting checklist for typosquatting
            CreateIndex(table: "PackageRegistrations", columns: new[] { "IsVerified", "DownloadCount"}, name: "IX_PackageRegistration_IsVerified_DownloadCount");
        }
        
        public override void Down()
        {
            DropIndex("PackageRegistrations", name: "IX_PackageRegistration_IsVerified_DownloadCount");
        }
    }
}
