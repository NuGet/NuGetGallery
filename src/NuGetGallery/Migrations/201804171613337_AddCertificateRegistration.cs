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
            
            AddColumn("dbo.Certificates", "Sha1Thumbprint", c => c.String(maxLength: 40, unicode: false));
            AddColumn("dbo.PackageRegistrations", "RequiredSignerKey", c => c.Int());
            AddColumn("dbo.Packages", "UserCertificateKey", c => c.Int());
            CreateIndex("dbo.PackageRegistrations", "RequiredSignerKey");
            CreateIndex("dbo.Packages", "UserCertificateKey");
            AddForeignKey("dbo.Packages", "UserCertificateKey", "dbo.UserCertificates", "Key");
            AddForeignKey("dbo.PackageRegistrations", "RequiredSignerKey", "dbo.Users", "Key");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.UserCertificates", "CertificateKey", "dbo.Certificates");
            DropForeignKey("dbo.UserCertificates", "UserKey", "dbo.Users");
            DropForeignKey("dbo.PackageRegistrations", "RequiredSignerKey", "dbo.Users");
            DropForeignKey("dbo.Packages", "UserCertificateKey", "dbo.UserCertificates");
            DropIndex("dbo.Packages", new[] { "UserCertificateKey" });
            DropIndex("dbo.PackageRegistrations", new[] { "RequiredSignerKey" });
            DropIndex("dbo.UserCertificates", new[] { "UserKey" });
            DropIndex("dbo.UserCertificates", new[] { "CertificateKey" });
            DropColumn("dbo.Packages", "UserCertificateKey");
            DropColumn("dbo.PackageRegistrations", "RequiredSignerKey");
            DropColumn("dbo.Certificates", "Sha1Thumbprint");
            DropTable("dbo.UserCertificates");
        }
    }
}
