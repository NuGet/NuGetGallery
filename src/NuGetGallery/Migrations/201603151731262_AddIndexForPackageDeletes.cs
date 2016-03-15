using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class AddIndexForPackageDeletes : DbMigration
    {
        public override void Up()
        {
            Sql("CREATE NONCLUSTERED INDEX [nci_wi_Packages_Deleted] ON [dbo].[Packages] ([Deleted], [Listed]) INCLUDE ([Description], [FlattenedDependencies], [IsPrerelease], [PackageRegistrationKey], [Tags], [Version]) WITH (ONLINE = ON)");
        }

        public override void Down()
        {
            Sql("DROP INDEX [nci_wi_Packages_Deleted] ON [dbo].[Packages]");
        }
    }
}
