namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class AddKeyOnSymbolsServerRequests : DbMigration
    {
        public override void Up()
        {
            DropPrimaryKey("dbo.SymbolsServerRequests");
            AddColumn("dbo.SymbolsServerRequests", "Key", c => c.Int(nullable: false, identity: true));
            AddPrimaryKey("dbo.SymbolsServerRequests", "Key");
            CreateIndex("dbo.SymbolsServerRequests", "SymbolsKey", unique: true, name: "IX_SymbolServerRequests_SymbolsKey");
        }
        
        public override void Down()
        {
            DropIndex("dbo.SymbolsServerRequests", "IX_SymbolServerRequests_SymbolsKey");
            DropPrimaryKey("dbo.SymbolsServerRequests");
            DropColumn("dbo.SymbolsServerRequests", "Key");
            AddPrimaryKey("dbo.SymbolsServerRequests", "SymbolsKey");
        }
    }
}
