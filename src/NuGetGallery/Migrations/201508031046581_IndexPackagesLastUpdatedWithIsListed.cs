namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class IndexPackagesLastUpdatedWithIsListed : DbMigration
    {
        public override void Up()
        {
            Sql("CREATE NONCLUSTERED INDEX [IX_Packages_LastUpdatedWithIsListed] ON[dbo].[Packages] ([LastUpdated]) INCLUDE([Listed], [Published])");
        }
        
        public override void Down()
        {
            DropIndex("Packages", name: "IX_Packages_LastUpdatedWithIsListed");
        }
    }
}
