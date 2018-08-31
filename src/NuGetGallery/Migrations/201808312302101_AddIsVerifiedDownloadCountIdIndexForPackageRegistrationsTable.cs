namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddIsVerifiedDownloadCountIdIndexForPackageRegistrationsTable : DbMigration
    {
        public override void Up()
        {
            // Used for getting checklist for typosquatting
            Sql("CREATE NONCLUSTERED INDEX [IX_PackageRegistration_IsVerified_DownloadCount] ON [dbo].[PackageRegistrations] ([IsVerified], [DownloadCount]) INCLUDE ([Id])");
        }
        
        public override void Down()
        {
            DropIndex("PackageRegistrations", name: "IX_PackageRegistration_IsVerified_DownloadCount");
        }
    }
}
