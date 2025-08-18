namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddCustomDataToPackageTypes : DbMigration
    {
        public override void Up()
        {
            AddColumn(
                "dbo.PackageTypes",
                "CustomData",
                c => c.String()
            );
        }

        public override void Down()
        {
            DropColumn("dbo.PackageTypes", "CustomData");
        }
    }
}
