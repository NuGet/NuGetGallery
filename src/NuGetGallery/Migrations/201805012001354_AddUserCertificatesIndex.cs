namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddUserCertificatesIndex : DbMigration
    {
        public override void Up()
        {
            DropIndex("dbo.UserCertificates", new[] { "CertificateKey" });
            DropIndex("dbo.UserCertificates", new[] { "UserKey" });
            CreateIndex("dbo.UserCertificates", new[] { "CertificateKey", "UserKey" }, unique: true, name: "IX_UserCertificates_CertificateKeyUserKey");
        }
        
        public override void Down()
        {
            DropIndex("dbo.UserCertificates", "IX_UserCertificates_CertificateKeyUserKey");
            CreateIndex("dbo.UserCertificates", "UserKey");
            CreateIndex("dbo.UserCertificates", "CertificateKey");
        }
    }
}
