namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddAccountDelete : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.AccountDeletes",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        DeletedOn = c.DateTime(nullable: false),
                        DeletedAccountKey = c.Int(nullable: false),
                        DeletedByKey = c.Int(nullable: false),
                        Signature = c.String(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Users", t => t.DeletedAccountKey, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.DeletedByKey)
                .Index(t => t.DeletedAccountKey)
                .Index(t => t.DeletedByKey);
            
            AddColumn("dbo.Users", "IsDeleted", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.AccountDeletes", "DeletedByKey", "dbo.Users");
            DropForeignKey("dbo.AccountDeletes", "DeletedAccountKey", "dbo.Users");
            DropIndex("dbo.AccountDeletes", new[] { "DeletedByKey" });
            DropIndex("dbo.AccountDeletes", new[] { "DeletedAccountKey" });
            DropColumn("dbo.Users", "IsDeleted");
            DropTable("dbo.AccountDeletes");
        }
    }
}
