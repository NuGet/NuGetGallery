namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddTriggerForPackagesLastEdited : DbMigration
    {
        public override void Up()
        {
            Sql(@"
CREATE TRIGGER [dbo].[LastEditedTrigger] ON [dbo].[Packages]
AFTER UPDATE
AS
BEGIN
    IF (UPDATE(LastEdited))
    BEGIN
        UPDATE [dbo].[Packages]
        SET LastEdited = GETUTCDATE()
        FROM INSERTED
        WHERE [dbo].[Packages].[Key] = INSERTED.[Key]
    END
END");
        }

        public override void Down()
        {
            Sql(@"DROP TRIGGER [dbo].[LastEditedTrigger]");
        }
    }
}
