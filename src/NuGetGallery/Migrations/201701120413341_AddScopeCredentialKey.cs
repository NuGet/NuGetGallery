namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddScopeCredentialKey : DbMigration
    {
        public override void Up()
        {
            RenameColumn(table: "dbo.Scopes", name: "Credential_Key", newName: "CredentialKey");
            RenameIndex(table: "dbo.Scopes", name: "IX_Credential_Key", newName: "IX_CredentialKey");
        }
        
        public override void Down()
        {
            RenameIndex(table: "dbo.Scopes", name: "IX_CredentialKey", newName: "IX_Credential_Key");
            RenameColumn(table: "dbo.Scopes", name: "CredentialKey", newName: "Credential_Key");
        }
    }
}
