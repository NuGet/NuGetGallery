namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class FixDeletedByCascades : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.PackageDeletes", "DeletedByKey", "dbo.Users");
            DropIndex("dbo.PackageDeletes", new[] { "DeletedByKey" });
            DropIndex("dbo.AccountDeletes", new[] { "DeletedByKey" });
            AlterColumn("dbo.PackageDeletes", "DeletedByKey", c => c.Int());
            AlterColumn("dbo.AccountDeletes", "DeletedByKey", c => c.Int());
            CreateIndex("dbo.PackageDeletes", "DeletedByKey");
            CreateIndex("dbo.AccountDeletes", "DeletedByKey");
            AddForeignKey("dbo.PackageDeletes", "DeletedByKey", "dbo.Users", "Key");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.PackageDeletes", "DeletedByKey", "dbo.Users");
            DropIndex("dbo.AccountDeletes", new[] { "DeletedByKey" });
            DropIndex("dbo.PackageDeletes", new[] { "DeletedByKey" });
            AlterColumn("dbo.AccountDeletes", "DeletedByKey", c => c.Int(nullable: false));
            AlterColumn("dbo.PackageDeletes", "DeletedByKey", c => c.Int(nullable: false));
            CreateIndex("dbo.AccountDeletes", "DeletedByKey");
            CreateIndex("dbo.PackageDeletes", "DeletedByKey");
            AddForeignKey("dbo.PackageDeletes", "DeletedByKey", "dbo.Users", "Key", cascadeDelete: true);
        }
    }
}
