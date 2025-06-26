namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class UpdatePackagesPackageRegistrationKeyIndex : DbMigration
    {
        public override void Up()
        {
            // There's an existing index that prevents altering these columns. We'll drop the index and recreate it.
            DropIndex(table: "Packages", name: "IX_Packages_PackageRegistrationKey");

            // CreateIndex does not support INCLUDE
            Sql(@"CREATE NONCLUSTERED INDEX [IX_Packages_PackageRegistrationKey] ON [dbo].[Packages]
                (
	                [PackageRegistrationKey] ASC
                )
                INCLUDE(
                       [Key]
                      ,[Copyright]
                      ,[Created]
                      ,[Description]
                      ,[DownloadCount]
                      ,[ExternalPackageUrl]
                      ,[HashAlgorithm]
                      ,[Hash]
                      ,[IconUrl]
                      ,[IsLatest]
                      ,[LastUpdated]
                      ,[LicenseUrl]
                      ,[Published]
                      ,[PackageFileSize]
                      ,[ProjectUrl]
                      ,[RequiresLicenseAcceptance]
                      ,[Summary]
                      ,[Tags]
                      ,[Title]
                      ,[Version]
                      ,[FlattenedAuthors]
                      ,[FlattenedDependencies]
                      ,[IsLatestStable]
                      ,[Listed]
                      ,[IsPrerelease]
                      ,[ReleaseNotes]
                      ,[Language]
                      ,[MinClientVersion]
                      ,[UserKey]
                      ,[LastEdited]
                      ,[HideLicenseReport]
                      ,[LicenseNames]
                      ,[LicenseReportUrl]
                      ,[NormalizedVersion]
                      ,[Deleted]
                      ,[PackageDelete_Key]
                      ,[FlattenedPackageTypes]
                      ,[SemVerLevelKey]
                      ,[IsLatestSemVer2]
                      ,[IsLatestStableSemVer2]
                      ,[RepositoryUrl]
                      ,[HasReadMe]
                      ,[PackageStatusKey]
                      ,[CertificateKey]
                      ,[RepositoryType]
                      ,[EmbeddedLicenseType]
                      ,[LicenseExpression]
                      ,[DevelopmentDependency]
                      ,[HasEmbeddedIcon]
                      ,[EmbeddedReadmeType]
                      ,[Id]
                )");
        }

        public override void Down()
        {
        }
    }
}
