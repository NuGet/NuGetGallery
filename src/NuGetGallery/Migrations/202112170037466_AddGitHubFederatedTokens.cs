namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddGitHubFederatedTokens : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.GitHubFederatedTokens",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        Organization = c.String(nullable: false),
                        Repository = c.String(nullable: false),
                        Branch = c.String(nullable: false),
                        UserKey = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Users", t => t.UserKey, cascadeDelete: true)
                .Index(t => t.UserKey);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.GitHubFederatedTokens", "UserKey", "dbo.Users");
            DropIndex("dbo.GitHubFederatedTokens", new[] { "UserKey" });
            DropTable("dbo.GitHubFederatedTokens");
        }
    }
}
