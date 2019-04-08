namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class DeletingAccountShouldDeleteUser : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.AccountDeletes", "Username", c => c.String());

            // Move usernames of deleted accounts from Users table into AccountDeletes table
            Sql(@"
UPDATE d
SET d.[Username] = u.[Username]
FROM AccountDeletes d
JOIN Users u ON d.[DeletedAccountKey] = u.[Key]");

            DropForeignKey("dbo.AccountDeletes", "DeletedAccountKey", "dbo.Users");
            DropForeignKey("dbo.PackageDeletes", "DeletedByKey", "dbo.Users");
            DropIndex("dbo.PackageDeletes", new[] { "DeletedByKey" });
            DropIndex("dbo.AccountDeletes", new[] { "DeletedAccountKey" });
            DropIndex("dbo.AccountDeletes", new[] { "DeletedByKey" });
            AlterColumn("dbo.PackageDeletes", "DeletedByKey", c => c.Int());
            AlterColumn("dbo.AccountDeletes", "DeletedByKey", c => c.Int());
            CreateIndex("dbo.AccountDeletes", "DeletedByKey");
            CreateIndex("dbo.PackageDeletes", "DeletedByKey");
            AddForeignKey("dbo.PackageDeletes", "DeletedByKey", "dbo.Users", "Key");
            DropColumn("dbo.Users", "IsDeleted");
            DropColumn("dbo.AccountDeletes", "DeletedAccountKey");
        }
        
        public override void Down()
        {
            AddColumn("dbo.AccountDeletes", "DeletedAccountKey", c => c.Int(nullable: false));
            AddColumn("dbo.Users", "IsDeleted", c => c.Boolean(nullable: false));
            DropForeignKey("dbo.PackageDeletes", "DeletedByKey", "dbo.Users");
            DropIndex("dbo.PackageDeletes", new[] { "DeletedByKey" });
            DropIndex("dbo.AccountDeletes", new[] { "DeletedByKey" });
            AlterColumn("dbo.AccountDeletes", "DeletedByKey", c => c.Int(nullable: false));
            AlterColumn("dbo.PackageDeletes", "DeletedByKey", c => c.Int(nullable: false));
            DropColumn("dbo.AccountDeletes", "Username");
            CreateIndex("dbo.AccountDeletes", "DeletedByKey");
            CreateIndex("dbo.AccountDeletes", "DeletedAccountKey");
            CreateIndex("dbo.PackageDeletes", "DeletedByKey");
            AddForeignKey("dbo.PackageDeletes", "DeletedByKey", "dbo.Users", "Key", cascadeDelete: true);
            AddForeignKey("dbo.AccountDeletes", "DeletedAccountKey", "dbo.Users", "Key", cascadeDelete: true);
        }
    }
}
