namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddSemVerDeletedIndex : DbMigration
    {
        public override void Up()
        {
            Sql("IF NOT EXISTS(SELECT * FROM sys.indexes WHERE name = 'nci_wi_Packages_SemVerDeleted' AND object_id = OBJECT_ID('Credentials')) CREATE NONCLUSTERED INDEX [nci_wi_Packages_SemVerDeleted] ON [dbo].[Packages]([SemVerLevelKey] ASC,[Deleted] ASC) INCLUDE ([PackageRegistrationKey],[Listed],[Tags],[Title]) ");
        }
        
        public override void Down()
        {
            Sql("DROP INDEX [nci_wi_Packages_SemVerDeleted] ON [dbo].[Packages]");
        }
    }
}
