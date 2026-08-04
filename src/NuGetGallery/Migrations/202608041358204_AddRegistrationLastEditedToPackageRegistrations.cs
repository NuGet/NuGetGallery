namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddRegistrationLastEditedToPackageRegistrations : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.PackageRegistrations", "RegistrationLastEdited", c => c.DateTime());
        }
        
        public override void Down()
        {
            DropColumn("dbo.PackageRegistrations", "RegistrationLastEdited");
        }
    }
}
