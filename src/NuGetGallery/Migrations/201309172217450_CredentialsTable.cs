namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class CredentialsTable : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Credentials",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        UserKey = c.Int(nullable: false),
                        Type = c.String(maxLength: 64),
                        Identifier = c.String(maxLength: 256),
                        Value = c.String(maxLength: 256),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Users", t => t.UserKey, cascadeDelete: true)
                .Index(t => t.UserKey);
            
        }
        
        public override void Down()
        {
            DropIndex("dbo.Credentials", new[] { "UserKey" });
            DropForeignKey("dbo.Credentials", "UserKey", "dbo.Users");
            DropTable("dbo.Credentials");
        }
    }
}
