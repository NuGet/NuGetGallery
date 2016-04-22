using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class AddIndexForPackageDeletes : DbMigration
    {
        public override void Up()
        {
            Sql("IF NOT EXISTS(SELECT * FROM sys.indexes WHERE name = 'nci_wi_Packages_Deleted' AND object_id = OBJECT_ID('Packages')) CREATE NONCLUSTERED INDEX [nci_wi_Packages_Deleted] ON [dbo].[Packages] ([Deleted], [Listed]) INCLUDE ([Description], [FlattenedDependencies], [IsPrerelease], [PackageRegistrationKey], [Tags], [Version])");
        }

        public override void Down()
        {
            Sql("DROP INDEX [nci_wi_Packages_Deleted] ON [dbo].[Packages]");
        }
    }
}
