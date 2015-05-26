namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class UniquePackageRegistration : DbMigration
    {
        public override void Up()
        {
            CreateIndex("PackageRegistrations", "Id", unique: true, name: "IX_PackageRegistration_Id_Unique");
        }

        public override void Down()
        {
            DropIndex("PackageRegistrations", name: "IX_PackageRegistration_Id_Unique");
        }
    }
}
