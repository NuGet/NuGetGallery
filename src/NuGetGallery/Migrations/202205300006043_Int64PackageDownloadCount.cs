namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class Int64PackageDownloadCount : DbMigration
    {
        public override void Up()
        {
            // Drop indices that depend on DownloadCount before we modify it.
            DropIndex(table: "PackageRegistrations", name: "IX_PackageRegistration_Id");
            DropIndex("PackageRegistrations", name: "IX_PackageRegistrations_Id_DownloadCount_Key");
            DropIndex("PackageRegistrations", name: "IX_PackageRegistration_IsVerified_DownloadCount");
            DropIndex(table: "Packages", name: "IX_Packages_PackageRegistrationKey");

            // Modify DownloadCount column to long
            AlterColumn("dbo.PackageRegistrations", "DownloadCount", c => c.Long(nullable: false));
            AlterColumn("dbo.Packages", "DownloadCount", c => c.Long(nullable: false));

            // Recreate the indices that were dropped
            Sql(@"Create Unique Index IX_PackageRegistration_Id on [dbo].[PackageRegistrations] (DownloadCount desc, Id asc) 
                         Include ([Key])");
            Sql("CREATE NONCLUSTERED INDEX [IX_PackageRegistrations_Id_DownloadCount_Key] ON [dbo].[PackageRegistrations] ([Id]) INCLUDE ([DownloadCount], [Key])");
            Sql("CREATE NONCLUSTERED INDEX [IX_PackageRegistration_IsVerified_DownloadCount] ON [dbo].[PackageRegistrations] ([IsVerified], [DownloadCount]) INCLUDE ([Id])");
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
            // Drop indices that depend on DownloadCount before we modify it.
            DropIndex(table: "PackageRegistrations", name: "IX_PackageRegistration_Id");
            DropIndex("PackageRegistrations", name: "IX_PackageRegistrations_Id_DownloadCount_Key");
            DropIndex("PackageRegistrations", name: "IX_PackageRegistration_IsVerified_DownloadCount");
            DropIndex(table: "Packages", name: "IX_Packages_PackageRegistrationKey");

            AlterColumn("dbo.Packages", "DownloadCount", c => c.Int(nullable: false));
            AlterColumn("dbo.PackageRegistrations", "DownloadCount", c => c.Int(nullable: false));

            // Recreate the indices that were dropped
            Sql(@"Create Unique Index IX_PackageRegistration_Id on [dbo].[PackageRegistrations] (DownloadCount desc, Id asc) 
                         Include ([Key])");
            Sql("CREATE NONCLUSTERED INDEX [IX_PackageRegistrations_Id_DownloadCount_Key] ON [dbo].[PackageRegistrations] ([Id]) INCLUDE ([DownloadCount], [Key])");
            Sql("CREATE NONCLUSTERED INDEX [IX_PackageRegistration_IsVerified_DownloadCount] ON [dbo].[PackageRegistrations] ([IsVerified], [DownloadCount]) INCLUDE ([Id])");
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
    }
}
