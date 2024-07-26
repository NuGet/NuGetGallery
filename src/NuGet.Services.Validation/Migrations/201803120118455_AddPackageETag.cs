namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class AddPackageETag : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.PackageValidationSets", "PackageETag", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.PackageValidationSets", "PackageETag");
        }
    }
}
