namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Contract_RemovePackageAuthorsTable : DbMigration
    {
        public override void Up()
        {
            DropIndex(table: "PackageAuthors", name: "IX_PackageAuthors_PackageKey");
            DropForeignKey("PackageAuthors", "PackageKey", "Packages", "Key");
            DropTable("PackageAuthors");
        }
        
        public override void Down()
        {
            CreateTable(
                "PackageAuthors",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        PackageKey = c.Int(nullable: false),
                        Name = c.String(),
                    })
                .PrimaryKey(t => t.Key);

            AddForeignKey("PackageAuthors", "PackageKey", "Packages", "Key");
            Sql("CREATE NONCLUSTERED INDEX [IX_PackageAuthors_PackageKey] ON [PackageAuthors] ([PackageKey]) INCLUDE ([Key],[Name])");
            // Note, at this point you aren't back to where you were before you ran the migration - all your data has been dropped.
            // If you need the data you should restore from backup instead.
        }
    }
}
