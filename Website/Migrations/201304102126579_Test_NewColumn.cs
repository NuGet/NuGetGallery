namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class Test_NewColumn : DbMigration
    {
        public override void Up()
        {
            AddColumn("Users", "NewColumn", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("Users", "NewColumn");
        }
    }
}
