namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class SecurityPoliciesFix : DbMigration
    {
        public override void Up()
        {
            DropUserSecurityPoliciesIfExists();

            CreateTable(
                "dbo.UserSecurityPolicies",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        UserKey = c.Int(nullable: false),
                        Name = c.String(nullable: false, maxLength: 256),
                        Value = c.String(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Users", t => t.UserKey, cascadeDelete: true)
                .Index(t => t.UserKey);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.UserSecurityPolicies", "UserKey", "dbo.Users");
            DropIndex("dbo.UserSecurityPolicies", new[] { "UserKey" });
            DropTable("dbo.UserSecurityPolicies");
        }

        private void DropUserSecurityPoliciesIfExists()
        {
            try
            {
                DropForeignKey("dbo.UserSecurityPolicies", "UserKey", "dbo.Users");
                DropTable("dbo.UserSecurityPolicies");
            }
            catch (Exception) { }
        }
    }
}
