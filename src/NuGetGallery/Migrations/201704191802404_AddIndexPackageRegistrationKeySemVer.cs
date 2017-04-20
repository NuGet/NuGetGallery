namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddIndexPackageRegistrationKeySemVer : DbMigration
    {
        public override void Up()
        {
            Sql("IF NOT EXISTS(SELECT * FROM sys.indexes WHERE name = 'nci_wi_Packages_PackageRegKeySemVer' AND object_id = OBJECT_ID('Packages')) " +
                "CREATE NONCLUSTERED INDEX [nci_wi_Packages_PackageRegKeySemVer] ON [dbo].[Packages]([PackageRegistrationKey] ASC, [SemVerLevelKey] ASC ) ");
        }

        public override void Down()
        {
            Sql("DROP INDEX [nci_wi_Packages_PackageRegKeySemVer] ON [dbo].[Packages]");
        }
    }
}
