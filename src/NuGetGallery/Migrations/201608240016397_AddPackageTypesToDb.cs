namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddPackageTypesToDb : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.PackageTypes",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        PackageKey = c.Int(nullable: false),
                        Name = c.String(maxLength: 512),
                        Version = c.String(maxLength: 128),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Packages", t => t.PackageKey, cascadeDelete: true)
                .Index(t => t.PackageKey);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.PackageTypes", "PackageKey", "dbo.Packages");
            DropIndex("dbo.PackageTypes", new[] { "PackageKey" });
            DropTable("dbo.PackageTypes");
        }
    }
}
