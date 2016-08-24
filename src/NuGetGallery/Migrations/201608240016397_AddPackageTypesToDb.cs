namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddPackageTypesToDb : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.PackageTypeEntities",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        PackageKey = c.Int(nullable: false),
                        Name = c.String(),
                        Version = c.String(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Packages", t => t.PackageKey, cascadeDelete: true)
                .Index(t => t.PackageKey);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.PackageTypeEntities", "PackageKey", "dbo.Packages");
            DropIndex("dbo.PackageTypeEntities", new[] { "PackageKey" });
            DropTable("dbo.PackageTypeEntities");
        }
    }
}
