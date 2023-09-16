namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddRepoBranchAndCommit : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "RepositoryBranch", c => c.String(maxLength: 150));
            AddColumn("dbo.Packages", "RepositoryCommit", c => c.String(maxLength: 100));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Packages", "RepositoryCommit");
            DropColumn("dbo.Packages", "RepositoryBranch");
        }
    }
}
