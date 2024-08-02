namespace NuGet.Services.Validation
{
    using System.Data.Entity.Migrations;

    public partial class AddPackageRevalidationEnqueuedCompletedIndex : DbMigration
    {
        public override void Up()
        {
            DropIndex("dbo.PackageRevalidations", "IX_PackageRevalidations_Enqueued");
            CreateIndex("dbo.PackageRevalidations", new[] { "Enqueued", "Completed" }, name: "IX_PackageRevalidations_Enqueued_Completed");
        }
        
        public override void Down()
        {
            DropIndex("dbo.PackageRevalidations", "IX_PackageRevalidations_Enqueued_Completed");
            CreateIndex("dbo.PackageRevalidations", "Enqueued", name: "IX_PackageRevalidations_Enqueued");
        }
    }
}
