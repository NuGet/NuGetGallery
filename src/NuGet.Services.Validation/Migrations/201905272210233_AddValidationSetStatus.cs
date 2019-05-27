namespace NuGet.Services.Validation
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddValidationSetStatus : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.PackageValidationSets", "ValidationSetStatus", c => c.Int(nullable: false, defaultValue: 0));
        }
        
        public override void Down()
        {
            DropColumn("dbo.PackageValidationSets", "ValidationSetStatus");
        }
    }
}
