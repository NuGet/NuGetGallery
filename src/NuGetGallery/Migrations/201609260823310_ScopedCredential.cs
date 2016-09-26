namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class ScopedCredential : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Scopes",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        Subject = c.String(),
                        AllowedAction = c.String(nullable: false),
                        Credential_Key = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Credentials", t => t.Credential_Key, cascadeDelete: true)
                .Index(t => t.Credential_Key);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Scopes", "Credential_Key", "dbo.Credentials");
            DropIndex("dbo.Scopes", new[] { "Credential_Key" });
            DropTable("dbo.Scopes");
        }
    }
}
