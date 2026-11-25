namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    /// <summary>
    /// Adds staging publication timestamps and adoption indexes for packages and symbol packages.
    /// </summary>
    public partial class AddPublishedFromStaging : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "PublishedFromStagingAt", c => c.DateTime());
            AddColumn("dbo.SymbolPackages", "PublishedFromStagingAt", c => c.DateTime());

            Sql(@"IF SERVERPROPERTY('edition') = 'SQL Azure'
                  BEGIN
                      EXECUTE sp_executesql N'CREATE NONCLUSTERED INDEX [IX_Packages_PublishedFromStagingAt]
                      ON [dbo].[Packages] ([PublishedFromStagingAt])
                      INCLUDE ([PackageRegistrationKey])
                      WHERE [PublishedFromStagingAt] IS NOT NULL
                      WITH (ONLINE = ON)'
                  END
                  ELSE
                  BEGIN
                      CREATE NONCLUSTERED INDEX [IX_Packages_PublishedFromStagingAt]
                      ON [dbo].[Packages] ([PublishedFromStagingAt])
                      INCLUDE ([PackageRegistrationKey])
                      WHERE [PublishedFromStagingAt] IS NOT NULL
                  END");

            Sql(@"IF SERVERPROPERTY('edition') = 'SQL Azure'
                  BEGIN
                      EXECUTE sp_executesql N'CREATE NONCLUSTERED INDEX [IX_SymbolPackages_PublishedFromStagingAt]
                      ON [dbo].[SymbolPackages] ([PublishedFromStagingAt])
                      INCLUDE ([PackageKey])
                      WHERE [PublishedFromStagingAt] IS NOT NULL
                      WITH (ONLINE = ON)'
                  END
                  ELSE
                  BEGIN
                      CREATE NONCLUSTERED INDEX [IX_SymbolPackages_PublishedFromStagingAt]
                      ON [dbo].[SymbolPackages] ([PublishedFromStagingAt])
                      INCLUDE ([PackageKey])
                      WHERE [PublishedFromStagingAt] IS NOT NULL
                  END");
        }
        
        public override void Down()
        {
            DropIndex(table: "dbo.SymbolPackages", name: "IX_SymbolPackages_PublishedFromStagingAt");
            DropIndex(table: "dbo.Packages", name: "IX_Packages_PublishedFromStagingAt");

            DropColumn("dbo.SymbolPackages", "PublishedFromStagingAt");
            DropColumn("dbo.Packages", "PublishedFromStagingAt");
        }
    }
}
