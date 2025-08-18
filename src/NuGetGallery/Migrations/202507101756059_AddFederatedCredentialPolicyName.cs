namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddFederatedCredentialPolicyName : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.FederatedCredentialPolicies", "PolicyName", c => c.String(maxLength: 64));
        }
        
        public override void Down()
        {
            DropColumn("dbo.FederatedCredentialPolicies", "PolicyName");
        }
    }
}
