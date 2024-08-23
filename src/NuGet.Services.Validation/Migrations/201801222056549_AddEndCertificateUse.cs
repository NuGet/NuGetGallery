namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class AddEndCertificateUse : DbMigration
    {
        public override void Up()
        {
            AddColumn("signature.EndCertificates", "Use", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("signature.EndCertificates", "Use");
        }
    }
}
