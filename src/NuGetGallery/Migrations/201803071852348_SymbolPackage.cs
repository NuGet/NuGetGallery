namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class SymbolPackage : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.SymbolPackages",
                c => new
                    {
                        Key = c.Int(nullable: false),
                        PackageRegistrationKey = c.Int(nullable: false),
                        Created = c.DateTime(nullable: false),
                        Title = c.String(maxLength: 256),
                        Version = c.String(nullable: false, maxLength: 64),
                        NormalizedVersion = c.String(maxLength: 64),
                        UserKey = c.Int(),
                        PackageStatusKey = c.Int(nullable: false),
                        DownloadCount = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.PackageRegistrations", t => t.PackageRegistrationKey, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.UserKey)
                .ForeignKey("dbo.Packages", t => t.Key, cascadeDelete: true)
                .Index(t => t.Key)
                .Index(t => t.PackageRegistrationKey)
                .Index(t => t.UserKey);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.SymbolPackages", "Key", "dbo.Packages");
            DropForeignKey("dbo.SymbolPackages", "UserKey", "dbo.Users");
            DropForeignKey("dbo.SymbolPackages", "PackageRegistrationKey", "dbo.PackageRegistrations");
            DropIndex("dbo.SymbolPackages", new[] { "UserKey" });
            DropIndex("dbo.SymbolPackages", new[] { "PackageRegistrationKey" });
            DropIndex("dbo.SymbolPackages", new[] { "Key" });
            DropTable("dbo.SymbolPackages");
        }
    }
}
