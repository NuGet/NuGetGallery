using System.Data.Entity.Migrations;

namespace NuGetGallery.Areas.Admin
{
    public partial class PackageInfoIsOptional : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.Issues", "PackageId", c => c.String(maxLength: 300, unicode: false));
            AlterColumn("dbo.Issues", "PackageVersion", c => c.String(maxLength: 300, unicode: false));
        }

        public override void Down()
        {
            AlterColumn("dbo.Issues", "PackageVersion", c => c.String(nullable: false, maxLength: 300, unicode: false));
            AlterColumn("dbo.Issues", "PackageId", c => c.String(nullable: false, maxLength: 300, unicode: false));
        }
    }
}
