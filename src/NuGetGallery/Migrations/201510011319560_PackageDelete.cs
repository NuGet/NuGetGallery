namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class PackageDelete : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.PackageDeletes",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        DeletedOn = c.DateTime(nullable: false),
                        DeletedByKey = c.Int(nullable: false),
                        Reason = c.String(nullable: false),
                        Signature = c.String(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Users", t => t.DeletedByKey, cascadeDelete: true)
                .Index(t => t.DeletedByKey);
            
            AddColumn("dbo.Packages", "Deleted", c => c.Boolean(nullable: false));
            AddColumn("dbo.Packages", "PackageDelete_Key", c => c.Int());
            CreateIndex("dbo.Packages", "PackageDelete_Key");
            AddForeignKey("dbo.Packages", "PackageDelete_Key", "dbo.PackageDeletes", "Key");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Packages", "PackageDelete_Key", "dbo.PackageDeletes");
            DropForeignKey("dbo.PackageDeletes", "DeletedByKey", "dbo.Users");
            DropIndex("dbo.PackageDeletes", new[] { "DeletedByKey" });
            DropIndex("dbo.Packages", new[] { "PackageDelete_Key" });
            DropColumn("dbo.Packages", "PackageDelete_Key");
            DropColumn("dbo.Packages", "Deleted");
            DropTable("dbo.PackageDeletes");
        }
    }
}
