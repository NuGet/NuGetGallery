namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class AddValidatorIssues : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.ValidatorIssues",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        ValidationId = c.Guid(nullable: false),
                        IssueCode = c.Int(nullable: false),
                        Data = c.String(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.ValidatorStatuses", t => t.ValidationId, cascadeDelete: true)
                .Index(t => t.ValidationId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.ValidatorIssues", "ValidationId", "dbo.ValidatorStatuses");
            DropIndex("dbo.ValidatorIssues", new[] { "ValidationId" });
            DropTable("dbo.ValidatorIssues");
        }
    }
}
