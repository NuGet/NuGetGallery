namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddStagingBlobCleanups : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.StagingBlobCleanups",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        StagedPackageIdentityKey = c.Int(nullable: false),
                        BlobPath = c.String(nullable: false, maxLength: 256),
                        BlobETag = c.String(nullable: false, maxLength: 256),
                        QueuedDate = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                    })
                .PrimaryKey(t => t.Key);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.StagingBlobCleanups");
        }
    }
}
