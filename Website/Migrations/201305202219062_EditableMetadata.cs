namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class EditableMetadata : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Tags",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 64),
                        Description = c.String(maxLength: 1024),
                    })
                .PrimaryKey(t => t.Key);
            
            CreateTable(
                "dbo.PackageTags",
                c => new
                    {
                        PackageRegistrationKey = c.Int(nullable: false),
                        TagKey = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.PackageRegistrationKey, t.TagKey })
                .ForeignKey("dbo.PackageRegistrations", t => t.PackageRegistrationKey, cascadeDelete: true)
                .ForeignKey("dbo.Tags", t => t.TagKey, cascadeDelete: true)
                .Index(t => t.PackageRegistrationKey)
                .Index(t => t.TagKey);
            
            AddColumn("dbo.PackageRegistrations", "DefaultTitle", c => c.String(maxLength: 256));
            AddColumn("dbo.PackageRegistrations", "Description", c => c.String());
            AddColumn("dbo.PackageRegistrations", "Summary", c => c.String(maxLength: 1024));
            AddColumn("dbo.PackageRegistrations", "IconUrl", c => c.String(maxLength: 256));
            AddColumn("dbo.PackageRegistrations", "ProjectUrl", c => c.String(maxLength: 256));
            AddColumn("dbo.PackageRegistrations", "SourceCodeUrl", c => c.String(maxLength: 256));
            AddColumn("dbo.PackageRegistrations", "IssueTrackerUrl", c => c.String(maxLength: 256));
            AddColumn("dbo.PackageRegistrations", "LastUpdated", c => c.DateTime(nullable: false));
            AddColumn("dbo.PackageRegistrations", "FlattenedTags", c => c.String(maxLength: 1024));
        }
        
        public override void Down()
        {
            DropIndex("dbo.PackageTags", new[] { "TagKey" });
            DropIndex("dbo.PackageTags", new[] { "PackageRegistrationKey" });
            DropForeignKey("dbo.PackageTags", "TagKey", "dbo.Tags");
            DropForeignKey("dbo.PackageTags", "PackageRegistrationKey", "dbo.PackageRegistrations");
            DropColumn("dbo.PackageRegistrations", "FlattenedTags");
            DropColumn("dbo.PackageRegistrations", "LastUpdated");
            DropColumn("dbo.PackageRegistrations", "IssueTrackerUrl");
            DropColumn("dbo.PackageRegistrations", "SourceCodeUrl");
            DropColumn("dbo.PackageRegistrations", "ProjectUrl");
            DropColumn("dbo.PackageRegistrations", "IconUrl");
            DropColumn("dbo.PackageRegistrations", "Summary");
            DropColumn("dbo.PackageRegistrations", "Description");
            DropColumn("dbo.PackageRegistrations", "DefaultTitle");
            DropTable("dbo.PackageTags");
            DropTable("dbo.Tags");
        }
    }
}
