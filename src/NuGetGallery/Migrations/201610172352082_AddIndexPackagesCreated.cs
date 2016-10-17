namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddIndexPackagesCreated : DbMigration
    {
        public override void Up()
        {
            Sql("IF NOT EXISTS(SELECT * FROM sys.indexes WHERE name = 'nci_wi_Packages_Created' AND object_id = OBJECT_ID('Packages')) CREATE NONCLUSTERED INDEX [nci_wi_Packages_Created] ON [dbo].[Packages] ([Created]) INCLUDE ([NormalizedVersion], [PackageRegistrationKey]) WITH (ONLINE = ON)");
        }
        
        public override void Down()
        {
            Sql("DROP INDEX [nci_wi_Packages_Created] ON [dbo].[Packages]");
        }
    }
}
