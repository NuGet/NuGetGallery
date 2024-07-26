namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class AddSymbolsRequests : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.SymbolsServerRequests",
                c => new
                    {
                        SymbolsKey = c.Int(nullable: false),
                        RequestName = c.String(nullable: false),
                        RequestStatusKey = c.Int(nullable: false),
                        Created = c.DateTime(nullable: false),
                        LastUpdated = c.DateTime(nullable: false),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.SymbolsKey);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.SymbolsServerRequests");
        }
    }
}
