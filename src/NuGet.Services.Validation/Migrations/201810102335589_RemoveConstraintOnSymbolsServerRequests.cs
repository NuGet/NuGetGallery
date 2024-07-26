namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class RemoveConstraintOnSymbolsServerRequests : DbMigration
    {
        public override void Up()
        {
            DropIndex("dbo.SymbolsServerRequests", "IX_SymbolServerRequests_SymbolsKey");
            CreateIndex("dbo.SymbolsServerRequests", "SymbolsKey", name: "IX_SymbolServerRequests_SymbolsKey");
        }
        
        public override void Down()
        {
            DropIndex("dbo.SymbolsServerRequests", "IX_SymbolServerRequests_SymbolsKey");
            CreateIndex("dbo.SymbolsServerRequests", "SymbolsKey", unique: true, name: "IX_SymbolServerRequests_SymbolsKey");
        }
    }
}
