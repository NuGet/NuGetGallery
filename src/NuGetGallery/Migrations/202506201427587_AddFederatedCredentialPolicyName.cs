namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddFederatedCredentialPolicyName : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.FederatedCredentialPolicies", "PolicyName", c => c.String(nullable: true, maxLength: 256));
        }

        public override void Down()
        {
            DropColumn("dbo.FederatedCredentialPolicies", "PolicyName");
        }
    }
}
