namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddDurableIdentityValues : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.DurableIdentityValues",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        Value = c.String(nullable: false, maxLength: 256, unicode: false),
                        Subject = c.String(),
                        ShortSubject = c.String(),
                        Issuer = c.String(),
                        ShortIssuer = c.String(),
                    })
                .PrimaryKey(t => t.Key)
                .Index(t => t.Value, unique: true, name: "IX_DurableIdentityValues_Value");
            
            CreateTable(
                "dbo.UserDurableIdentityValues",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        DurableIdentityValueKey = c.Int(nullable: false),
                        UserKey = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Users", t => t.UserKey, cascadeDelete: true)
                .ForeignKey("dbo.DurableIdentityValues", t => t.DurableIdentityValueKey, cascadeDelete: true)
                .Index(t => new { t.DurableIdentityValueKey, t.UserKey }, unique: true, name: "IX_UserDurableIdentityValues_DurableIdentityValueKeyUserKey");
            
            AddColumn("dbo.Certificates", "DurableIdentityValueKey", c => c.Int());
            CreateIndex("dbo.Certificates", "DurableIdentityValueKey");
            AddForeignKey("dbo.Certificates", "DurableIdentityValueKey", "dbo.DurableIdentityValues", "Key");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.UserDurableIdentityValues", "DurableIdentityValueKey", "dbo.DurableIdentityValues");
            DropForeignKey("dbo.UserDurableIdentityValues", "UserKey", "dbo.Users");
            DropForeignKey("dbo.Certificates", "DurableIdentityValueKey", "dbo.DurableIdentityValues");
            DropIndex("dbo.UserDurableIdentityValues", "IX_UserDurableIdentityValues_DurableIdentityValueKeyUserKey");
            DropIndex("dbo.DurableIdentityValues", "IX_DurableIdentityValues_Value");
            DropIndex("dbo.Certificates", new[] { "DurableIdentityValueKey" });
            DropColumn("dbo.Certificates", "DurableIdentityValueKey");
            DropTable("dbo.UserDurableIdentityValues");
            DropTable("dbo.DurableIdentityValues");
        }
    }
}
