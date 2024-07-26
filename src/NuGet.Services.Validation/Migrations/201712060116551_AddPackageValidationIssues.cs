namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class AddPackageValidationIssues : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.PackageValidationIssues",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        PackageValidationKey = c.Guid(nullable: false),
                        IssueCode = c.Int(nullable: false),
                        Data = c.String(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.PackageValidations", t => t.PackageValidationKey, cascadeDelete: true)
                .Index(t => t.PackageValidationKey);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.PackageValidationIssues", "PackageValidationKey", "dbo.PackageValidations");
            DropIndex("dbo.PackageValidationIssues", new[] { "PackageValidationKey" });
            DropTable("dbo.PackageValidationIssues");
        }
    }
}
