namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddIsLatestSemVer2Index : DbMigration
    {
        public override void Up()
        {
            // "WITH (ONLINE = ON)" is not supported on all editions of SQL Server. We want to create the index in the background
            // when we are deploying to our live environment on Azure (which supports online index creation).
            // Editions: https://docs.microsoft.com/en-us/sql/t-sql/functions/serverproperty-transact-sql?view=sql-server-ver15#arguments
            // We used sp_executesql because it is blocked on SQL that does not support "WITH (ONLINE = ON)".
            Sql(@"IF SERVERPROPERTY ('edition') = 'SQL Azure'
                BEGIN
                    EXECUTE sp_executesql N'CREATE NONCLUSTERED INDEX [IX_Packages_IsLatestSemVer2] ON [dbo].[Packages] ([IsLatestSemVer2] ASC)
                    INCLUDE([PackageRegistrationKey]) 
                    WITH (ONLINE = ON)'
                END
                ELSE
                BEGIN
                    CREATE NONCLUSTERED INDEX [IX_Packages_IsLatestSemVer2] ON [dbo].[Packages] ([IsLatestSemVer2] ASC)
                    INCLUDE([PackageRegistrationKey])
                END");
        }
        
        public override void Down()
        {
            DropIndex(table: "Packages", name: "IX_Packages_IsLatestSemVer2");
        }
    }
}
