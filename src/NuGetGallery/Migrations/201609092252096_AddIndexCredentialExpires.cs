namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class AddIndexCredentialExpires : DbMigration
    {
        public override void Up()
        {
            Sql("IF NOT EXISTS(SELECT * FROM sys.indexes WHERE name = 'nci_wi_Credentials_Type_Expires' AND object_id = OBJECT_ID('Credentials')) CREATE NONCLUSTERED INDEX [nci_wi_Credentials_Type_Expires] ON [dbo].[Credentials] ([Type], [Expires]) INCLUDE ([Created], [UserKey])");
        }

        public override void Down()
        {
            Sql("DROP INDEX [nci_wi_Credentials_Type_Expires] ON [dbo].[Credentials]");
        }
    }
}
