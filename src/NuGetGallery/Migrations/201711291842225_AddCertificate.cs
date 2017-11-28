namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddCertificate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Certificates",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        Thumbprint = c.String(nullable: false, maxLength: 256, unicode: false),
                    })
                .PrimaryKey(t => t.Key)
                .Index(t => t.Thumbprint, unique: true, name: "IX_Certificates_Thumbprint");
            
        }
        
        public override void Down()
        {
            DropIndex("dbo.Certificates", "IX_Certificates_Thumbprint");
            DropTable("dbo.Certificates");
        }
    }
}
