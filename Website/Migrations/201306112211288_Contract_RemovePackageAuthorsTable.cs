namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Contract_RemovePackageAuthorsTable : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.PackageAuthors", "PackageKey", "dbo.Packages");
            DropIndex("dbo.PackageAuthors", new[] { "PackageKey" });
            DropTable("dbo.PackageAuthors");
        }
        
        public override void Down()
        {
            CreateTable(
                "dbo.PackageAuthors",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        PackageKey = c.Int(nullable: false),
                        Name = c.String(),
                    })
                .PrimaryKey(t => t.Key);
            
            CreateIndex("dbo.PackageAuthors", "PackageKey");
            AddForeignKey("dbo.PackageAuthors", "PackageKey", "dbo.Packages", "Key", cascadeDelete: true);
            // Note, at this point you aren't back to where you were before you ran the migration - all your data has been dropped.
            // If you need the data you should restore from backup instead.
        }
    }
}
