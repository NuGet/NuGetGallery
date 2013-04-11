namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Test_AnotherColumn : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Users", "AnotherNewColumn", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Users", "AnotherNewColumn");
        }
    }
}
