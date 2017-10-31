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
                        Signature = c.String(nullable: false),
                        DeletedAccount_Key = c.Int(nullable: false),
                        DeletedBy_Key = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Users", t => t.DeletedAccount_Key, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.DeletedBy_Key)
                .Index(t => t.DeletedAccount_Key)
                .Index(t => t.DeletedBy_Key);
            
            AddColumn("dbo.Users", "IsDeleted", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.AccountDeletes", "DeletedBy_Key", "dbo.Users");
            DropForeignKey("dbo.AccountDeletes", "DeletedAccount_Key", "dbo.Users");
            DropIndex("dbo.AccountDeletes", new[] { "DeletedBy_Key" });
            DropIndex("dbo.AccountDeletes", new[] { "DeletedAccount_Key" });
            DropColumn("dbo.Users", "IsDeleted");
            DropTable("dbo.AccountDeletes");
        }
    }
}
