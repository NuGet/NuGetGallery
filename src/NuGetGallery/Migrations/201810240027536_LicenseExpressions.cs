namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class LicenseExpressions : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "LicenseExpression", c => c.String(maxLength: 500));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Packages", "LicenseExpression");
        }
    }
}
