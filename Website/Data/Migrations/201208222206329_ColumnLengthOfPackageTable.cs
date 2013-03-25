using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class ColumnLengthOfPackageTable : DbMigration
    {
        public override void Up()
        {
            // There's an existing index that prevents altering these columns. We'll drop the index and recreate it.
            DropIndex(table: "Packages", name: "IX_Packages_PackageRegistrationKey");

            AlterColumn("PackageRegistrations", "Id", c => c.String(nullable: false, maxLength: 128));
            AlterColumn("Packages", "HashAlgorithm", c => c.String(maxLength: 10));
            AlterColumn("Packages", "Hash", c => c.String(nullable: false, maxLength: 256));
            AlterColumn("Packages", "Title", c => c.String(maxLength: 256));
            AlterColumn("Packages", "Version", c => c.String(nullable: false, maxLength: 64));
            AlterColumn("PackageDependencies", "Id", c => c.String(maxLength: 128));
            AlterColumn("PackageDependencies", "VersionSpec", c => c.String(maxLength: 256));
            AlterColumn("PackageDependencies", "TargetFramework", c => c.String(maxLength: 256));
            AlterColumn("PackageFrameworks", "TargetFramework", c => c.String(maxLength: 256));

            // CreateIndex does not support INCLUDE
            Sql(@"CREATE NONCLUSTERED INDEX [IX_Packages_PackageRegistrationKey] ON [dbo].[Packages] 
                (
                    [PackageRegistrationKey] ASC
                )
                INCLUDE ( [Key],
                [Copyright],
                [Created],
                [Description],
                [DownloadCount],
                [ExternalPackageUrl],
                [HashAlgorithm],
                [Hash],
                [IconUrl],
                [IsLatest],
                [LastUpdated],
                [LicenseUrl],
                [Published],
                [PackageFileSize],
                [ProjectUrl],
                [RequiresLicenseAcceptance],
                [Summary],
                [Tags],
                [Title],
                [Version],
                [FlattenedAuthors],
                [FlattenedDependencies],
                [IsLatestStable],
                [Listed],
                [IsPrerelease],
                [ReleaseNotes])");
        }

        public override void Down()
        {
            AlterColumn("PackageFrameworks", "TargetFramework", c => c.String());
            AlterColumn("PackageDependencies", "TargetFramework", c => c.String());
            AlterColumn("PackageDependencies", "VersionSpec", c => c.String());
            AlterColumn("PackageDependencies", "Id", c => c.String());
            AlterColumn("Packages", "Version", c => c.String());
            AlterColumn("Packages", "Title", c => c.String());
            AlterColumn("Packages", "Hash", c => c.String());
            AlterColumn("Packages", "HashAlgorithm", c => c.String());
            AlterColumn("PackageRegistrations", "Id", c => c.String());
        }
    }
}