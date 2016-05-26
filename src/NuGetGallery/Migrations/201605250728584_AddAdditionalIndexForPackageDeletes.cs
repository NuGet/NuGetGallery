using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class AddAdditionalIndexForPackageDeletes : DbMigration
    {
        public override void Up()
        {
            Sql("IF NOT EXISTS(SELECT * FROM sys.indexes WHERE name = 'nci_wi_Packages_IsListedDeleted' AND object_id = OBJECT_ID('Packages')) CREATE NONCLUSTERED INDEX [nci_wi_Packages_IsListedDeleted] ON [dbo].[Packages] ([IsLatest], [Deleted]) INCLUDE ([PackageRegistrationKey], [Tags], [Title])");
            Sql("IF NOT EXISTS(SELECT * FROM sys.indexes WHERE name = 'nci_wi_Packages_IsLatestStableDeleted' AND object_id = OBJECT_ID('Packages')) CREATE NONCLUSTERED INDEX [nci_wi_Packages_IsLatestStableDeleted] ON [dbo].[Packages] ([IsLatestStable], [Deleted]) INCLUDE ([Description], [FlattenedAuthors], [Listed], [PackageRegistrationKey], [Published], [Tags])");
        }

        public override void Down()
        {
            Sql("DROP INDEX [nci_wi_Packages_IsLatestStableDeleted] ON [dbo].[Packages]");
            Sql("DROP INDEX [nci_wi_Packages_IsListedDeleted] ON [dbo].[Packages]");
        }
    }
}