using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class AddIndexForPackageLicenseReportsPackageKey : DbMigration
    {
        public override void Up()
        {
            Sql("IF NOT EXISTS(SELECT * FROM sys.indexes WHERE name = 'nci_wi_PackageLicenseReports_PackageKey' AND object_id = OBJECT_ID('PackageLicenseReports')) CREATE NONCLUSTERED INDEX [nci_wi_PackageLicenseReports_PackageKey] ON [dbo].[PackageLicenseReports] ([PackageKey]) INCLUDE ([Comment], [CreatedUtc], [Key], [ReportUrl], [Sequence])");
        }

        public override void Down()
        {
            Sql("DROP INDEX [nci_wi_PackageLicenseReports_PackageKey] ON [dbo].[PackageLicenseReports]");
        }
    }
}
