namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class AddPackageCompatibilityIssuesTable : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.PackageCompatibilityIssues",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        ClientIssueCode = c.String(nullable: false),
                        Message = c.String(nullable: false),
                        PackageValidationKey = c.Guid(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.PackageValidations", t => t.PackageValidationKey, cascadeDelete: true)
                .Index(t => t.PackageValidationKey);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.PackageCompatibilityIssues", "PackageValidationKey", "dbo.PackageValidations");
            DropIndex("dbo.PackageCompatibilityIssues", new[] { "PackageValidationKey" });
            DropTable("dbo.PackageCompatibilityIssues");
        }
    }
}
