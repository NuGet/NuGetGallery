namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddCertificateDetails : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Certificates", "Subject", c => c.String());
            AddColumn("dbo.Certificates", "Issuer", c => c.String());
            AddColumn("dbo.Certificates", "Expiration", c => c.DateTime(precision: 7, storeType: "datetime2"));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Certificates", "Expiration");
            DropColumn("dbo.Certificates", "Issuer");
            DropColumn("dbo.Certificates", "Subject");
        }
    }
}
