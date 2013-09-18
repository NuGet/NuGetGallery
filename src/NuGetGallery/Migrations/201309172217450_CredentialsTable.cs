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
                        Type = c.String(nullable: false, maxLength: 64),
                        Identifier = c.String(maxLength: 256),
                        Value = c.String(nullable: false, maxLength: 256),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Users", t => t.UserKey, cascadeDelete: true)
                .Index(t => t.UserKey);
            
            CreateIndex(
                "dbo.Credentials",
                new[] { "Type", "Value" },
                unique: true,
                name: "IX_Credentials_Type_Value");

            AlterColumn("dbo.Users", "ApiKey", c => c.Guid());
        }
        
        public override void Down()
        {
            DropIndex("dbo.Credentials", new[] { "UserKey" });
            DropIndex("dbo.Credentials", "IX_Credentials_Type_Value");

            DropForeignKey("dbo.Credentials", "UserKey", "dbo.Users");
            AlterColumn("dbo.Users", "ApiKey", c => c.Guid(nullable: false));
            DropTable("dbo.Credentials");
        }
    }
}
