namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AllowOrphanedStagingBlobCleanups : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.StagingBlobCleanups", "StagedPackageIdentityKey", c => c.Int());
        }
        
        public override void Down()
        {
            AlterColumn("dbo.StagingBlobCleanups", "StagedPackageIdentityKey", c => c.Int(nullable: false));
        }
    }
}
