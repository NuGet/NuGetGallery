namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddIndexSemVerLevelKeyPackageRegistrationKey : DbMigration
    {
        public override void Up()
        {
            Sql("IF NOT EXISTS(SELECT * FROM sys.indexes WHERE name = 'nci_wi_Packages_SemVerLevelKey_PackageRegistrationKey' AND object_id = OBJECT_ID('Packages')) CREATE NONCLUSTERED INDEX [nci_wi_Packages_SemVerLevelKey_PackageRegistrationKey] ON [dbo].[Packages] ([SemVerLevelKey],[IsPrerelease],[Deleted]) INCLUDE ([PackageRegistrationKey],[Description],[Tags])");
        }
        
        public override void Down()
        {
            Sql("DROP INDEX [nci_wi_Packages_SemVerLevelKey_PackageRegistrationKey] ON [dbo].[Packages]");
        }
    }
}
