namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class DropCreateDbOptionOnSymbolServerRequest : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.SymbolsServerRequests", "Created", c => c.DateTime(nullable: false));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.SymbolsServerRequests", "Created", c => c.DateTime(nullable: false));
        }
    }
}
