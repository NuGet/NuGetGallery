using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class AddIndexForCredentialsUserKey : DbMigration
    {
        public override void Up()
        {
            Sql("IF NOT EXISTS(SELECT * FROM sys.indexes WHERE name = 'nci_wi_Credentials_UserKey' AND object_id = OBJECT_ID('Credentials')) CREATE NONCLUSTERED INDEX [nci_wi_Credentials_UserKey] ON [dbo].[Credentials] ([UserKey]) INCLUDE ([Identity], [Key], [Type], [Value])");
        }

        public override void Down()
        {
            Sql("DROP INDEX [nci_wi_Credentials_UserKey] ON [dbo].[Credentials]");
        }
    }
}
