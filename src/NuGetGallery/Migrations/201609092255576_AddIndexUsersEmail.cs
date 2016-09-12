namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class AddIndexUsersEmail : DbMigration
    {
        public override void Up()
        {
            Sql("IF NOT EXISTS(SELECT * FROM sys.indexes WHERE name = 'nci_wi_Users_EmailAllowed' AND object_id = OBJECT_ID('Users')) CREATE NONCLUSTERED INDEX [nci_wi_Users_EmailAllowed] ON [dbo].[Users] ([EmailAllowed], [EmailAddress]) INCLUDE ([Key], [Username])");
        }

        public override void Down()
        {
            Sql("DROP INDEX [nci_wi_Users_EmailAllowed] ON [dbo].[Users]");
        }
    }
}
