namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AnonymousUploadEntities : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.PackageRegistrations", "TemporaryId", c => c.String());
            AddColumn("dbo.Packages", "ClaimKey", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Packages", "ClaimKey");
            DropColumn("dbo.PackageRegistrations", "TemporaryId");
        }
    }
}
