namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class PackageMetadataEditable : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.PackageMetadatas",
                c => new
                    {
                        Key = c.Int(nullable: false),
                        PackageKey = c.Int(nullable: false),
                        EditName = c.String(maxLength: 64),
                        Timestamp = c.DateTime(nullable: false),
                        IsCompleted = c.Boolean(nullable: false),
                        TriedCount = c.Int(nullable: false),
                        Authors = c.String(),
                        Copyright = c.String(),
                        Description = c.String(),
                        Hash = c.String(),
                        HashAlgorithm = c.String(maxLength: 10),
                        IconUrl = c.String(),
                        LicenseUrl = c.String(),
                        PackageFileSize = c.Long(nullable: false),
                        ProjectUrl = c.String(),
                        ReleaseNotes = c.String(),
                        Summary = c.String(),
                        Tags = c.String(),
                        Title = c.String(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Packages", t => t.Key, cascadeDelete: true)
                .Index(t => t.Key);
            
            AddColumn("dbo.Packages", "MetadataKey", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropIndex("dbo.PackageMetadatas", new[] { "Key" });
            DropForeignKey("dbo.PackageMetadatas", "Key", "dbo.Packages");
            DropColumn("dbo.Packages", "MetadataKey");
            DropTable("dbo.PackageMetadatas");
        }
    }
}
