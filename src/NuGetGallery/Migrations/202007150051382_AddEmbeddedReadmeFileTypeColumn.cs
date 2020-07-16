namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class ReadmeChanges : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "EmbeddedReadmeFileType", c => c.Int(nullable: false));
        }

        public override void Down()
        {
            DropColumn("dbo.Packages", "EmbeddedReadmeFileType");
        }
    }
}