namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class AddTrustedTimestampStatus : DbMigration
    {
        public override void Up()
        {
            AddColumn("signature.TrustedTimestamps", "Status", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("signature.TrustedTimestamps", "Status");
        }
    }
}
