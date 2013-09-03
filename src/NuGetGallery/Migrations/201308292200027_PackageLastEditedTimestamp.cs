namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class PackageLastEditedTimestamp : DbMigration
    {
        public override void Up()
        {
            AddColumn("Packages", "LastEdited", c => c.DateTime());
            CreateIndex("Packages", "LastEdited", name: "IX_Packages_LastEdited");
        }

        public override void Down()
        {
            DropIndex("Packages", "IX_Packages_LastEdited");
            DropColumn("Packages", "LastEdited");
        }
    }
}
