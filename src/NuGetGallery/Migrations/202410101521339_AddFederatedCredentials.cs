namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddFederatedCredentials : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.FederatedCredentialPolicies",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        UserKey = c.Int(nullable: false),
                        Created = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        LastUpdated = c.DateTime(precision: 7, storeType: "datetime2"),
                        LastMatched = c.DateTime(precision: 7, storeType: "datetime2"),
                        TypeKey = c.Int(nullable: false),
                        Criteria = c.String(nullable: false),
                        OwnerKey = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Users", t => t.OwnerKey)
                .ForeignKey("dbo.Users", t => t.UserKey)
                .Index(t => t.UserKey)
                .Index(t => t.OwnerKey);
            
            CreateTable(
                "dbo.FederatedCredentials",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        TypeKey = c.Int(nullable: false),
                        FederatedCredentialPolicyKey = c.Int(nullable: false),
                        Identity = c.String(maxLength: 64),
                        Created = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        Expires = c.DateTime(precision: 7, storeType: "datetime2"),
                    })
                .PrimaryKey(t => t.Key)
                .Index(t => t.FederatedCredentialPolicyKey)
                .Index(t => t.Identity, unique: true);
            
            AddColumn("dbo.Credentials", "FederatedCredentialPolicyKey", c => c.Int());
            CreateIndex("dbo.Credentials", "FederatedCredentialPolicyKey");
            AddForeignKey("dbo.Credentials", "FederatedCredentialPolicyKey", "dbo.FederatedCredentialPolicies", "Key");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.FederatedCredentialPolicies", "UserKey", "dbo.Users");
            DropForeignKey("dbo.FederatedCredentialPolicies", "OwnerKey", "dbo.Users");
            DropForeignKey("dbo.Credentials", "FederatedCredentialPolicyKey", "dbo.FederatedCredentialPolicies");
            DropIndex("dbo.FederatedCredentials", new[] { "Identity" });
            DropIndex("dbo.FederatedCredentials", new[] { "FederatedCredentialPolicyKey" });
            DropIndex("dbo.FederatedCredentialPolicies", new[] { "OwnerKey" });
            DropIndex("dbo.FederatedCredentialPolicies", new[] { "UserKey" });
            DropIndex("dbo.Credentials", new[] { "FederatedCredentialPolicyKey" });
            DropColumn("dbo.Credentials", "FederatedCredentialPolicyKey");
            DropTable("dbo.FederatedCredentials");
            DropTable("dbo.FederatedCredentialPolicies");
        }
    }
}
