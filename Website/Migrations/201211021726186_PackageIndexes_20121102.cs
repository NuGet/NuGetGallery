namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class PackageIndexes_20121102 : DbMigration
    {
        public override void Up()
        {
            // These were identified by SQL Azure as missing indexes
            Sql(@"CREATE INDEX IX_Package_LastUpdated ON [NuGetGallery].[dbo].[Packages] ([LastUpdated]) INCLUDE ([PackageRegistrationKey])");
            Sql(@"CREATE INDEX IX_Package_Prerelease ON [NuGetGallery].[dbo].[Packages] ([IsPrerelease])");
            Sql(@"CREATE INDEX IX_Package_Prerelease_WithTags ON [NuGetGallery].[dbo].[Packages] ([IsPrerelease]) INCLUDE ([Tags])");
            Sql(@"CREATE INDEX IX_Package_Prerelease_WithPackageRegistration ON [NuGetGallery].[dbo].[Packages] ([IsPrerelease]) INCLUDE ([PackageRegistrationKey])");
            Sql(@"CREATE INDEX IX_Package_Prerelease_WithPackageRegistrationDescription ON [NuGetGallery].[dbo].[Packages] ([IsPrerelease]) INCLUDE ([PackageRegistrationKey], [Description])");
            Sql(@"CREATE INDEX IX_Package_Prerelease_WithPackageRegistrationDescriptionTags ON [NuGetGallery].[dbo].[Packages] ([IsPrerelease]) INCLUDE ([PackageRegistrationKey], [Description], [Tags])");
            Sql(@"CREATE INDEX IX_Package_LatestStable_WithLastUpdated ON [NuGetGallery].[dbo].[Packages] ([IsLatestStable],[IsPrerelease]) INCLUDE ([PackageRegistrationKey], [LastUpdated])");
            Sql(@"CREATE INDEX IX_Package_LatestStableListed ON [NuGetGallery].[dbo].[Packages] ([IsLatestStable], [Listed],[IsPrerelease]) INCLUDE ([Key], [PackageRegistrationKey])");
            Sql(@"CREATE INDEX IX_Package_Version_WithNone ON [NuGetGallery].[dbo].[Packages] ([Version])");
            Sql(@"CREATE INDEX IX_Package_Latest_WithPackageRegistrationDescriptionTags ON [NuGetGallery].[dbo].[Packages] ([IsLatest]) INCLUDE ([PackageRegistrationKey], [Description], [Tags])");
            Sql(@"CREATE INDEX IX_Package_LatestListed_WithKeyPackageRegistration ON [NuGetGallery].[dbo].[Packages] ([IsLatest], [Listed]) INCLUDE ([Key], [PackageRegistrationKey])");
        }
        
        public override void Down()
        {
            DropIndex(table: "Packages", name: "IX_Package_LastUpdated");
            DropIndex(table: "Packages", name: "IX_Package_Prerelease");
            DropIndex(table: "Packages", name: "IX_Package_Prerelease_WithTags");
            DropIndex(table: "Packages", name: "IX_Package_Prerelease_WithPackageRegistration");
            DropIndex(table: "Packages", name: "IX_Package_Prerelease_WithPackageRegistrationDescription");
            DropIndex(table: "Packages", name: "IX_Package_Prerelease_WithPackageRegistrationDescriptionTags");
            DropIndex(table: "Packages", name: "IX_Package_LatestStable_WithLastUpdated");
            DropIndex(table: "Packages", name: "IX_Package_LatestStableListed");
            DropIndex(table: "Packages", name: "IX_Package_Version_WithNone");
            DropIndex(table: "Packages", name: "IX_Package_Latest_WithPackageRegistrationDescriptionTags");
            DropIndex(table: "Packages", name: "IX_Package_LatestListed_WithKeyPackageRegistration");
        }
    }
}
