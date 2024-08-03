namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class AddValidatingType : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.PackageValidationSets", "ValidatingType", c => c.Int(nullable: false));
            AddColumn("dbo.ValidatorStatuses", "ValidatingType", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.ValidatorStatuses", "ValidatingType");
            DropColumn("dbo.PackageValidationSets", "ValidatingType");
        }
    }
}
