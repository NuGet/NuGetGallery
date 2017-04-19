namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class SecurityPolicies : DbMigration
    {
        public override void Up()
        {
            CreateTable("SecurityPolicies", c => new
            {
                Key = c.Int(nullable: false, identity: true),
                Type = c.String(nullable: false, maxLength: 256),
                Value = c.String(nullable: false)
            })
            .PrimaryKey(t => t.Key);

            CreateTable(
                "UserSecurityPolicies",
                c => new
                {
                    SecurityPolicyKey = c.Int(nullable: false),
                    UserKey = c.Int(nullable: false),
                })
                .PrimaryKey(t => new { t.SecurityPolicyKey, t.UserKey })
                .ForeignKey("SecurityPolicies", t => t.SecurityPolicyKey)
                .ForeignKey("Users", t => t.UserKey);
        }
        
        public override void Down()
        {
            DropTable("UserSecurityPolicies");
            DropTable("SecurityPolicies");
        }
    }
}
