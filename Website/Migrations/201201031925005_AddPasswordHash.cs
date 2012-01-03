namespace NuGetGallery.Migrations.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class AddPasswordHash : DbMigration
    {
        public override void Up()
        {
            AddColumn("Users", "PasswordHashAlgorithm", c => c.String(nullable: false, defaultValue: "SHA1"));
        }
        
        public override void Down()
        {
            DropColumn("Users", "PasswordHashAlgorithm");
        }
    }
}
