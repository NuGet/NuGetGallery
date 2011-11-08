namespace NuGetGallery.Migrations.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class DisplayPackageIndexes : DbMigration
    {
        public override void Up()
        {
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
                [ReleaseNotes]) WITH (STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]");
        }
        
        public override void Down()
        {
            DropIndex(table: "Packages", name: "IX_Packages_PackageRegistrationKey");
        }
    }
}
