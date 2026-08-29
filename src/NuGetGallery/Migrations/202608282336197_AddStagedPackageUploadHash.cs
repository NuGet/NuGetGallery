namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddStagedPackageUploadHash : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.StagedPackages", "UploadHash", c => c.String(nullable: false, maxLength: 256));
        }
        
        public override void Down()
        {
            DropColumn("dbo.StagedPackages", "UploadHash");
        }
    }
}
