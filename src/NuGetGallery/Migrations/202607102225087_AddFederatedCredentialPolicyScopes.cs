namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddFederatedCredentialPolicyScopes : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Scopes", "FederatedCredentialPolicyKey", c => c.Int());
            CreateIndex("dbo.Scopes", "FederatedCredentialPolicyKey");
            AddForeignKey("dbo.Scopes", "FederatedCredentialPolicyKey", "dbo.FederatedCredentialPolicies", "Key", cascadeDelete: true);
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Scopes", "FederatedCredentialPolicyKey", "dbo.FederatedCredentialPolicies");
            DropIndex("dbo.Scopes", new[] { "FederatedCredentialPolicyKey" });
            DropColumn("dbo.Scopes", "FederatedCredentialPolicyKey");
        }
    }
}
