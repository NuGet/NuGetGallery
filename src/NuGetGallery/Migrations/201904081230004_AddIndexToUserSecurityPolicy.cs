namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddIndexToUserSecurityPolicy : DbMigration
    {
        public override void Up()
        {
            DropIndex("dbo.UserSecurityPolicies", new[] { "UserKey" });
            CreateIndex("dbo.UserSecurityPolicies", new[] { "UserKey", "Name", "Subscription" }, unique: true, name: "IX_UserSecurityPolicy_UserKeyNameSubscription");
        }
        
        public override void Down()
        {
            DropIndex("dbo.UserSecurityPolicies", "IX_UserSecurityPolicy_UserKeyNameSubscription");
            CreateIndex("dbo.UserSecurityPolicies", "UserKey");
        }
    }
}
