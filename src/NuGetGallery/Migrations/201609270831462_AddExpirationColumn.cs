namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddExpirationColumn : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Credentials", "ExpirationTicks", c => c.Long());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Credentials", "ExpirationTicks");
        }
    }
}
