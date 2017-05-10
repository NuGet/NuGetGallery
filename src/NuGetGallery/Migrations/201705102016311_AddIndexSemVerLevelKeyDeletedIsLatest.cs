namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddIndexSemVerLevelKeyDeletedIsLatest : DbMigration
    {
        private const string IndexName = "nci_Packages_SemVerIsLatestDeleted";

        public override void Up()
        {
            Sql($"IF NOT EXISTS(SELECT * FROM sys.indexes WHERE name = '{IndexName}' AND object_id = OBJECT_ID('Packages')) " +
                $"CREATE NONCLUSTERED INDEX [{IndexName}] ON [dbo].[Packages]([SemVerLevelKey], [IsLatest], [Deleted]) ");
        }

        public override void Down()
        {
            Sql($"DROP INDEX [{IndexName}] ON [dbo].[Packages]");
        }
    }
}
