namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class UpdateScopeCredentialKey : DbMigration
    {
        public override void Up()
        {
            DropIndex("dbo.Scopes", new[] { "CredentialKey" });
            AlterColumn("dbo.Scopes", "CredentialKey", c => c.Int());
            CreateIndex("dbo.Scopes", "CredentialKey");
        }
        
        public override void Down()
        {
            DropIndex("dbo.Scopes", new[] { "CredentialKey" });
            AlterColumn("dbo.Scopes", "CredentialKey", c => c.Int(nullable: false));
            CreateIndex("dbo.Scopes", "CredentialKey");
        }
    }
}
