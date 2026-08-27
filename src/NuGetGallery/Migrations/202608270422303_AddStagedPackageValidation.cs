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
            AddColumn("dbo.StagedPackages", "BlobETag", c => c.String(nullable: false, maxLength: 256));
            AddColumn("dbo.StagedPackages", "Status", c => c.Int(nullable: false));
            AddColumn("dbo.StagedPackages", "RowVersion", c => c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"));
            AddPrimaryKey("dbo.StagedPackages", "Key");
        }
        
        public override void Down()
        {
            DropPrimaryKey("dbo.StagedPackages");
            DropColumn("dbo.StagedPackages", "RowVersion");
            DropColumn("dbo.StagedPackages", "Status");
            DropColumn("dbo.StagedPackages", "BlobETag");
            DropColumn("dbo.StagedPackages", "Key");
            AddPrimaryKey("dbo.StagedPackages", "PackageKey");
        }
    }
}
