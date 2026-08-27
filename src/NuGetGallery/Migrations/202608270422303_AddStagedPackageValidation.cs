namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddStagedPackageValidation : DbMigration
    {
        public override void Up()
        {
            DropPrimaryKey("dbo.StagedPackages");
            AddColumn("dbo.StagedPackages", "Key", c => c.Int(nullable: false, identity: true));
            AddColumn("dbo.StagedPackages", "UploadedBlobETag", c => c.String(nullable: false, maxLength: 256));
            AddColumn("dbo.StagedPackages", "ValidatedBlobPath", c => c.String(maxLength: 256));
            AddColumn("dbo.StagedPackages", "ValidatedBlobETag", c => c.String(maxLength: 256));
            AddColumn("dbo.StagedPackages", "Status", c => c.Int(nullable: false));
            AddColumn("dbo.StagedPackages", "RowVersion", c => c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"));
            AddPrimaryKey("dbo.StagedPackages", "Key");
            RenameColumn("dbo.StagedPackages", "BlobPath", "UploadedBlobPath");
        }
        
        public override void Down()
        {
            RenameColumn("dbo.StagedPackages", "UploadedBlobPath", "BlobPath");
            DropPrimaryKey("dbo.StagedPackages");
            DropColumn("dbo.StagedPackages", "RowVersion");
            DropColumn("dbo.StagedPackages", "Status");
            DropColumn("dbo.StagedPackages", "ValidatedBlobETag");
            DropColumn("dbo.StagedPackages", "ValidatedBlobPath");
            DropColumn("dbo.StagedPackages", "UploadedBlobETag");
            DropColumn("dbo.StagedPackages", "Key");
            AddPrimaryKey("dbo.StagedPackages", "PackageKey");
        }
    }
}
