namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class AddValidatorStatusNupkgUrl : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.ValidatorStatuses", "NupkgUrl", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.ValidatorStatuses", "NupkgUrl");
        }
    }
}
