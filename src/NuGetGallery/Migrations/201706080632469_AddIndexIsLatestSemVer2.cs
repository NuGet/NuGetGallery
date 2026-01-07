using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class AddIndexIsLatestSemVer2 : DbMigration
    {
        public override void Up()
        {
            Sql("IF NOT EXISTS(SELECT * FROM sys.indexes WHERE name = 'nci_wi_Packages_IsLatestSemVer2' AND object_id = OBJECT_ID('Packages')) CREATE NONCLUSTERED INDEX [nci_wi_Packages_IsLatestSemVer2] ON [dbo].[Packages] ([SemVerLevelKey],[Deleted]) INCLUDE ([IsLatest],[IsLatestSemVer2])");
        }
        
        public override void Down()
        {
            Sql("DROP INDEX [nci_wi_Packages_IsLatestSemVer2] ON [dbo].[Packages]");
        }
    }
}
