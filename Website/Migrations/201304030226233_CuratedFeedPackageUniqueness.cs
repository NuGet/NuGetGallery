namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class CuratedFeedPackageUniqueness : DbMigration
    {
        public override void Up()
        {
            // DELETE duplicate CuratedPackages from the database
            // Trying to prefer keeping duplicated entries that were manually added, or were added with notes
            Sql(@"WITH NumberedRows
AS 
(
    SELECT Row_number() OVER 
        (PARTITION BY CuratedFeedKey, PackageRegistrationKey ORDER BY CuratedFeedKey, PackageRegistrationKey, AutomaticallyCurated, Notes DESC)
        RowId, * from CuratedPackages
)
DELETE FROM NumberedRows WHERE RowId > 1");

            // ADD uniqueness constraint - as an Index, since it seems like a reasonable way to look up curated packages
            CreateIndex("CuratedPackages", new[] { "CuratedFeedKey", "PackageRegistrationKey" }, unique: true, name: "IX_CuratedFeed_PackageRegistration");
        }
        
        public override void Down()
        {
            // REMOVE uniqueness constraint
            DropIndex("CuratedPackages", "IX_CuratedPackage_CuratedFeedAndPackageRegistration");
        }
    }
}
