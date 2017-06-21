namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RepositoryUrl : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "RepositoryUrl", c => c.String());
            AddColumn("dbo.PackageEdits", "RepositoryUrl", c => c.String());
            AddColumn("dbo.PackageEdits", "ReadmeModified", c => c.Boolean(nullable: false));
            AddColumn("dbo.PackageHistories", "RepositoryUrl", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.PackageHistories", "RepositoryUrl");
            DropColumn("dbo.PackageEdits", "ReadmeModified");
            DropColumn("dbo.PackageEdits", "RepositoryUrl");
            DropColumn("dbo.Packages", "RepositoryUrl");
        }
    }
}
