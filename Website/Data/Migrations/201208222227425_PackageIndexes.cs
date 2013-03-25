using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class PackageIndexes : DbMigration
    {
        public override void Up()
        {
            // Used by VS search.
            Sql(@"Create NonClustered Index IX_Package_Search On [dbo].[Packages] ([IsLatestStable],[IsLatest],[Listed],[IsPrerelease]) 
                         Include ([Key],[PackageRegistrationKey],[Description],[Summary],[Tags])");

            // Used in the package page
            Sql(@"Create NonClustered Index IX_PackageDependencies On [dbo].[PackageDependencies] ([PackageKey]) 
                        Include ([Key])");

            // Used for paging and sorting results
            CreateIndex(table: "Packages", columns: new[] { "PackageRegistrationKey", "Version" }, unique: true, name: "IX_Package_Version");

            // Adding an index on the package Id in PackageRegistrations
            Sql(@"Create Unique Index IX_PackageRegistration_Id on [dbo].[PackageRegistrations] (DownloadCount desc, Id asc) 
                         Include ([Key])");
        }

        public override void Down()
        {
            DropIndex(table: "Packages", name: "IX_Package_Search");
            DropIndex(table: "PackageDependencies", name: "IX_PackageDependencies");
            DropIndex(table: "Packages", name: "IX_Package_Version");
            DropIndex(table: "PackageRegistrations", name: "IX_PackageRegistration_Id");
        }
    }
}