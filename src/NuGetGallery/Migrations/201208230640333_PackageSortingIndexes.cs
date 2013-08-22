using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class PackageSortingIndexes : DbMigration
    {
        public override void Up()
        {
            // These indexes were found to be needed after the last deployment and then performing package searches
            Sql(@"Create NonClustered Index [IX_PackageRegistration_Id_Key] On [dbo].[PackageRegistrations] ([Id]) 
                         Include ([Key])");

            CreateIndex(table: "Packages", columns: new[] { "IsLatest", "Listed" }, name: "IX_Package_IsLatest");

            CreateIndex(table: "Packages", columns: new[] { "IsLatestStable", "Listed", "IsPrerelease" }, name: "IX_Package_IsLatestStable");

            Sql(@"Create NonClustered Index [IX_Package_Listed] On [dbo].[Packages] ([Listed]) 
                         Include ([PackageRegistrationKey],[IsLatest],[IsLatestStable])");
        }

        public override void Down()
        {
            DropIndex(table: "Packages", name: "IX_Package_Listed");
            DropIndex(table: "Packages", name: "IX_Package_IsLatestStable");
            DropIndex(table: "Packages", name: "IX_Package_IsLatest");
            DropIndex(table: "PackageRegistrations", name: "IX_PackageRegistration_Id_Key");
        }
    }
}