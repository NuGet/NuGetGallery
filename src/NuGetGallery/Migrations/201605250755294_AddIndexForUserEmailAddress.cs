using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class AddIndexForUserEmailAddress : DbMigration
    {
        public override void Up()
        {
            Sql("IF NOT EXISTS(SELECT * FROM sys.indexes WHERE name = 'nci_wi_Users_EmailAddress' AND object_id = OBJECT_ID('Users')) CREATE NONCLUSTERED INDEX [nci_wi_Users_EmailAddress] ON [dbo].[Users] ([EmailAddress])");
        }

        public override void Down()
        {
            Sql("DROP INDEX [nci_wi_Users_EmailAddress] ON [dbo].[Users]");
        }
    }
}
