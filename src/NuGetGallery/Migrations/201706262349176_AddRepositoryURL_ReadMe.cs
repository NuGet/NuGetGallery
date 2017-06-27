namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddRepositoryURL_ReadMe : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "RepositoryUrl", c => c.String());
            AddColumn("dbo.Packages", "HasReadMe", c => c.Boolean());
            AddColumn("dbo.PackageEdits", "RepositoryUrl", c => c.String());
            AddColumn("dbo.PackageEdits", "ReadMeState", c => c.String());
            AddColumn("dbo.PackageHistories", "RepositoryUrl", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.PackageHistories", "RepositoryUrl");
            DropColumn("dbo.PackageEdits", "ReadMeState");
            DropColumn("dbo.PackageEdits", "RepositoryUrl");
            DropColumn("dbo.Packages", "HasReadMe");
            DropColumn("dbo.Packages", "RepositoryUrl");
        }
    }
}
