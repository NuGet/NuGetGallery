namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class AddFederatedCredentialPolicyScopes : DbMigration
    {
        public override void Up()
        {
            DropIndex("dbo.Scopes", new[] { "CredentialKey" });
            AddColumn("dbo.Scopes", "FederatedCredentialPolicyKey", c => c.Int());
            AlterColumn("dbo.Scopes", "CredentialKey", c => c.Int());
            CreateIndex("dbo.Scopes", "CredentialKey");
            CreateIndex("dbo.Scopes", "FederatedCredentialPolicyKey");
            AddForeignKey("dbo.Scopes", "FederatedCredentialPolicyKey", "dbo.FederatedCredentialPolicies", "Key", cascadeDelete: true);

            Sql(@"ALTER TABLE [dbo].[Scopes] ADD CONSTRAINT CHK_CredentialKeyOrFederatedCredentialPolicyKeyNotNull CHECK (CredentialKey IS NOT NULL OR FederatedCredentialPolicyKey IS NOT NULL)");
        }

        public override void Down()
        {
            Sql(@"ALTER TABLE [dbo].[Scopes] DROP CONSTRAINT IF EXISTS [CHK_CredentialKeyOrFederatedCredentialPolicyKeyNotNull]");

            DropForeignKey("dbo.Scopes", "FederatedCredentialPolicyKey", "dbo.FederatedCredentialPolicies");
            DropIndex("dbo.Scopes", new[] { "FederatedCredentialPolicyKey" });
            DropIndex("dbo.Scopes", new[] { "CredentialKey" });
            AlterColumn("dbo.Scopes", "CredentialKey", c => c.Int(nullable: false));
            DropColumn("dbo.Scopes", "FederatedCredentialPolicyKey");
            CreateIndex("dbo.Scopes", "CredentialKey");
        }
    }
}
