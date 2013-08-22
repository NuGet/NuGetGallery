using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class MyPackagesIndexes : DbMigration
    {
        public override void Up()
        {
            // CreateIndex doesn't support INCLUDE
            Sql(
                "CREATE NONCLUSTERED INDEX [IX_PackageRegistrationOwners_UserKey] ON [dbo].[PackageRegistrationOwners] ([UserKey]) INCLUDE ([PackageRegistrationKey])");
        }

        public override void Down()
        {
            DropIndex(table: "PackageRegistrationOwners", name: "IX_PackageRegistrationOwners_UserKey");
        }
    }
}