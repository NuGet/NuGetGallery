namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddStagedPackageValidation : DbMigration
    {
        public override void Up()
        {
            Sql(@"
                DELETE p
                FROM [dbo].[Packages] AS p
                INNER JOIN [dbo].[StagedPackages] AS s ON s.[PackageKey] = p.[Key]");

            AddColumn("dbo.StagedPackages", "BlobETag", c => c.String(nullable: false, maxLength: 256));
            AddColumn("dbo.StagedPackages", "Status", c => c.Int(nullable: false));
            AddColumn("dbo.StagedPackages", "ValidationTrackingId", c => c.Guid(nullable: false));
            AddColumn("dbo.StagedPackages", "RowVersion", c => c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"));
        }
        
        public override void Down()
        {
            DropColumn("dbo.StagedPackages", "RowVersion");
            DropColumn("dbo.StagedPackages", "ValidationTrackingId");
            DropColumn("dbo.StagedPackages", "Status");
            DropColumn("dbo.StagedPackages", "BlobETag");
        }
    }
}
