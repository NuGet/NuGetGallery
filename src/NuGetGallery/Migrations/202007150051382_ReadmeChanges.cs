namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class ReadmeChanges : DbMigration
    {
        public override void Up()
        {
            Sql("ALTER TABLE [dbo].[Packages] ADD EmbeddedReadmeFileType INT NOT NULL CONSTRAINT DF_Doc_Exz_EmbeddedReadmeFileType DEFAULT 0");
        }

        public override void Down()
        {
            DropColumn("dbo.Packages", "EmbeddedReadmeFileType");
        }
    }
}