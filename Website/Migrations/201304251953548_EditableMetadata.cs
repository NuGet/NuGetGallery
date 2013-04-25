namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class EditableMetadata : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.PackageRegistrations", "DefaultTitle", c => c.String(maxLength: 256));
            AddColumn("dbo.PackageRegistrations", "Description", c => c.String());
            AddColumn("dbo.PackageRegistrations", "Summary", c => c.String(maxLength: 1024));
            AddColumn("dbo.PackageRegistrations", "IconUrl", c => c.String(maxLength: 256));
            AddColumn("dbo.PackageRegistrations", "ProjectUrl", c => c.String(maxLength: 256));
            AddColumn("dbo.PackageRegistrations", "SourceCodeUrl", c => c.String(maxLength: 256));
            AddColumn("dbo.PackageRegistrations", "IssueTrackerUrl", c => c.String(maxLength: 256));
            AddColumn("dbo.PackageRegistrations", "LastUpdated", c => c.DateTime(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.PackageRegistrations", "LastUpdated");
            DropColumn("dbo.PackageRegistrations", "IssueTrackerUrl");
            DropColumn("dbo.PackageRegistrations", "SourceCodeUrl");
            DropColumn("dbo.PackageRegistrations", "ProjectUrl");
            DropColumn("dbo.PackageRegistrations", "IconUrl");
            DropColumn("dbo.PackageRegistrations", "Summary");
            DropColumn("dbo.PackageRegistrations", "Description");
            DropColumn("dbo.PackageRegistrations", "DefaultTitle");
        }
    }
}
