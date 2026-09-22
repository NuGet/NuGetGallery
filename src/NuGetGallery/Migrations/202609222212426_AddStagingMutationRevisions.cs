namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddStagingMutationRevisions : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.StagedPackages", "MutationRevision", c => c.Long(nullable: false));
            AddColumn("dbo.StagingGroups", "MutationRevision", c => c.Long(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.StagingGroups", "MutationRevision");
            DropColumn("dbo.StagedPackages", "MutationRevision");
        }
    }
}
