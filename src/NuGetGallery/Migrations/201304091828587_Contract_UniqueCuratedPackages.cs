namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class Contract_UniqueCuratedPackages : DbMigration
    {
        public override void Up()
        {
            // ADD uniqueness constraint - as an Index, since it seems reasonable to look up curated package entries by their feed + registration
            CreateIndex("CuratedPackages", new[] { "CuratedFeedKey", "PackageRegistrationKey" }, unique: true, name: "IX_CuratedFeed_PackageRegistration");
        }

        public override void Down()
        {
            // REMOVE uniqueness constraint
            DropIndex("CuratedPackages", "IX_CuratedFeed_PackageRegistration");
        }
    }
}
