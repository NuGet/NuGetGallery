namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddPackageStatusKey : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "PackageStatusKey", c => c.Int(
                nullable: false,
                defaultValue: 0));

            Sql(@"
DECLARE @PerIteration INT = 1000;
DECLARE @Delay VARCHAR(8) = '00:00:01';

DECLARE @UpdateCount INT = -1;
WHILE @UpdateCount <> 0
BEGIN
    UPDATE TOP (@PerIteration) dbo.Packages
    SET PackageStatusKey = 1
    WHERE Deleted = 1 AND PackageStatusKey = 0

    SELECT @UpdateCount = @@ROWCOUNT

    WAITFOR DELAY @Delay
END
");

            Sql("IF NOT EXISTS(SELECT * FROM sys.indexes WHERE name = 'nci_wi_Packages_PackageStatusKeyListed' AND object_id = OBJECT_ID('Packages')) CREATE INDEX [nci_wi_Packages_PackageStatusKeyListed] ON [dbo].[Packages] ([PackageStatusKey] ASC, [Listed] ASC) INCLUDE ([Description], [FlattenedDependencies], [IsPrerelease], [PackageRegistrationKey], [Tags], [Version])");

            Sql("IF NOT EXISTS(SELECT * FROM sys.indexes WHERE name = 'nci_wi_Packages_IsListedPackageStatusKey' AND object_id = OBJECT_ID('Packages')) CREATE INDEX [nci_wi_Packages_IsListedPackageStatusKey] ON [dbo].[Packages] ([IsLatest] ASC, [PackageStatusKey] ASC) INCLUDE ([PackageRegistrationKey], [Tags], [Title])");

            Sql("IF NOT EXISTS(SELECT * FROM sys.indexes WHERE name = 'nci_wi_Packages_IsLatestStablePackageStatusKey' AND object_id = OBJECT_ID('Packages')) CREATE INDEX [nci_wi_Packages_IsLatestStablePackageStatusKey] ON [dbo].[Packages] ([IsLatestStable] ASC, [PackageStatusKey] ASC) INCLUDE ([Description], [FlattenedAuthors], [Listed], [PackageRegistrationKey], [Published], [Tags])");

            Sql("IF NOT EXISTS(SELECT * FROM sys.indexes WHERE name = 'nci_wi_Packages_SemVerLevelKeyIsLatestPackageStatusKey' AND object_id = OBJECT_ID('Packages')) CREATE INDEX [nci_wi_Packages_SemVerLevelKeyIsLatestPackageStatusKey] ON [dbo].[Packages] ([SemVerLevelKey] ASC, [IsLatest] ASC, [PackageStatusKey] ASC) INCLUDE ([PackageRegistrationKey])");

            Sql("IF NOT EXISTS(SELECT * FROM sys.indexes WHERE name = 'nci_wi_Packages_SemVerLevelKeyPackageStatusKey' AND object_id = OBJECT_ID('Packages')) CREATE INDEX [nci_wi_Packages_SemVerLevelKeyPackageStatusKey] ON [dbo].[Packages] ([SemVerLevelKey] ASC, [PackageStatusKey] ASC) INCLUDE ([IsLatest], [IsLatestSemVer2])");

            Sql("IF NOT EXISTS(SELECT * FROM sys.indexes WHERE name = 'nci_wi_Packages_PackageStatusKeyIsPrereleasePackageStatusKey' AND object_id = OBJECT_ID('Packages')) CREATE INDEX [nci_wi_Packages_PackageStatusKeyIsPrereleasePackageStatusKey] ON [dbo].[Packages] ([SemVerLevelKey] ASC, [IsPrerelease] ASC, [PackageStatusKey] ASC) INCLUDE ([PackageRegistrationKey], [Description], [Tags])");
        }
        
        public override void Down()
        {
            DropIndex(table: "Packages", name: "nci_wi_Packages_PackageStatusKeyListed");

            DropIndex(table: "Packages", name: "nci_wi_Packages_IsListedPackageStatusKey");

            DropIndex(table: "Packages", name: "nci_wi_Packages_IsLatestStablePackageStatusKey");

            DropIndex(table: "Packages", name: "nci_wi_Packages_SemVerLevelKeyIsLatestPackageStatusKey");

            DropIndex(table: "Packages", name: "nci_wi_Packages_SemVerLevelKeyPackageStatusKey");

            DropIndex(table: "Packages", name: "nci_wi_Packages_PackageStatusKeyIsPrereleasePackageStatusKey");

            DropColumn("dbo.Packages", "PackageStatusKey");
        }
    }
}
