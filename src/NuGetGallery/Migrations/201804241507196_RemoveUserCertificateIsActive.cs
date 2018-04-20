namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RemoveUserCertificateIsActive : DbMigration
    {
        public override void Up()
        {
            DropColumn("dbo.UserCertificates", "IsActive");
        }
        
        public override void Down()
        {
            AddColumn("dbo.UserCertificates", "IsActive", c => c.Boolean(nullable: false));
        }
    }
}
