namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class UserSecurityPolicies_SubscriptionColumn : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.UserSecurityPolicies", "Subscription", c => c.String(nullable: false, maxLength: 256));
        }
        
        public override void Down()
        {
            DropColumn("dbo.UserSecurityPolicies", "Subscription");
        }
    }
}
