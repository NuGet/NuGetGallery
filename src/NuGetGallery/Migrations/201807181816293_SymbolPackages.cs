namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class SymbolPackages : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.SymbolPackages",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        PackageKey = c.Int(nullable: false),
                        Created = c.DateTime(nullable: false),
                        Published = c.DateTime(),
                        FileSize = c.Long(nullable: false),
                        HashAlgorithm = c.String(maxLength: 10),
                        Hash = c.String(nullable: false, maxLength: 256),
                        StatusKey = c.Int(nullable: false),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Packages", t => t.PackageKey, cascadeDelete: true)
                .Index(t => t.PackageKey);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.SymbolPackages", "PackageKey", "dbo.Packages");
            DropIndex("dbo.SymbolPackages", new[] { "PackageKey" });
            DropTable("dbo.SymbolPackages");
        }
    }
}
