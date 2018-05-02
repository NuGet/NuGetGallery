namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddCertificateRegistration : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.UserCertificates",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        CertificateKey = c.Int(nullable: false),
                        UserKey = c.Int(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Users", t => t.UserKey, cascadeDelete: true)
                .ForeignKey("dbo.Certificates", t => t.CertificateKey, cascadeDelete: true)
                .Index(t => t.CertificateKey)
                .Index(t => t.UserKey);
            
            CreateTable(
                "dbo.PackageRegistrationRequiredSigners",
                c => new
                    {
                        PackageRegistrationKey = c.Int(nullable: false),
                        UserKey = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.PackageRegistrationKey, t.UserKey })
                .ForeignKey("dbo.PackageRegistrations", t => t.PackageRegistrationKey, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.UserKey, cascadeDelete: true)
                .Index(t => t.PackageRegistrationKey)
                .Index(t => t.UserKey);
            
            AddColumn("dbo.Certificates", "Sha1Thumbprint", c => c.String(maxLength: 40, unicode: false));
            AddColumn("dbo.Packages", "CertificateKey", c => c.Int());
            CreateIndex("dbo.Packages", "CertificateKey");
            AddForeignKey("dbo.Packages", "CertificateKey", "dbo.Certificates", "Key");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.UserCertificates", "CertificateKey", "dbo.Certificates");
            DropForeignKey("dbo.UserCertificates", "UserKey", "dbo.Users");
            DropForeignKey("dbo.PackageRegistrationRequiredSigners", "UserKey", "dbo.Users");
            DropForeignKey("dbo.PackageRegistrationRequiredSigners", "PackageRegistrationKey", "dbo.PackageRegistrations");
            DropForeignKey("dbo.Packages", "CertificateKey", "dbo.Certificates");
            DropIndex("dbo.PackageRegistrationRequiredSigners", new[] { "UserKey" });
            DropIndex("dbo.PackageRegistrationRequiredSigners", new[] { "PackageRegistrationKey" });
            DropIndex("dbo.Packages", new[] { "CertificateKey" });
            DropIndex("dbo.UserCertificates", new[] { "UserKey" });
            DropIndex("dbo.UserCertificates", new[] { "CertificateKey" });
            DropColumn("dbo.Packages", "CertificateKey");
            DropColumn("dbo.Certificates", "Sha1Thumbprint");
            DropTable("dbo.PackageRegistrationRequiredSigners");
            DropTable("dbo.UserCertificates");
        }
    }
}
