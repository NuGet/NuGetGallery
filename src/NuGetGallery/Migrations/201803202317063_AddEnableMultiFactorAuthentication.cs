namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddEnableMultiFactorAuthentication : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Users", "EnableMultiFactorAuthentication", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Users", "EnableMultiFactorAuthentication");
        }
    }
}
