namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddIndexSemVerLevelKey : DbMigration
    {
        public override void Up()
        {
            Sql("IF NOT EXISTS(SELECT * FROM sys.indexes WHERE name = 'nci_wi_Packages_SemVerLevelKey' AND object_id = OBJECT_ID('Packages')) CREATE NONCLUSTERED INDEX [nci_wi_Packages_SemVerLevelKey] ON [dbo].[Packages] ([SemVerLevelKey], [IsLatest], [Deleted]) INCLUDE ([PackageRegistrationKey])");
        }
        
        public override void Down()
        {
            Sql("DROP INDEX [nci_wi_Packages_SemVerLevelKey] ON [dbo].[Packages]");
        }
    }
}
