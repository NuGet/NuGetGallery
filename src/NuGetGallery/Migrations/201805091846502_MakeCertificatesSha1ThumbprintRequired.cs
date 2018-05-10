namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class MakeCertificatesSha1ThumbprintRequired : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.Certificates", "Sha1Thumbprint", c => c.String(nullable: false, maxLength: 40, unicode: false));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.Certificates", "Sha1Thumbprint", c => c.String(maxLength: 40, unicode: false));
        }
    }
}
