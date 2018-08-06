namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddRepositoryType : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "RepositoryType", c => c.String(maxLength: 100));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Packages", "RepositoryType");
        }
    }
}
