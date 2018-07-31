namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddShortCertificateNames : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Certificates", "ShortSubject", c => c.String());
            AddColumn("dbo.Certificates", "ShortIssuer", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Certificates", "ShortIssuer");
            DropColumn("dbo.Certificates", "ShortSubject");
        }
    }
}
