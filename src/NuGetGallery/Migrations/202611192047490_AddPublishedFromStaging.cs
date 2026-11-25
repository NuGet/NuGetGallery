namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    /// <summary>
    /// Adds publication-origin markers and adoption indexes for packages and symbol packages.
    /// </summary>
    public partial class AddPublishedFromStaging : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Packages", "PublishedFromStaging", c => c.Boolean(nullable: false));
            AddColumn("dbo.SymbolPackages", "PublishedFromStaging", c => c.Boolean(nullable: false));

            Sql(@"IF SERVERPROPERTY('edition') = 'SQL Azure'
                  BEGIN
                      EXECUTE sp_executesql N'CREATE NONCLUSTERED INDEX [IX_Packages_PublishedFromStaging]
                      ON [dbo].[Packages] ([PackageRegistrationKey])
                      WHERE [PublishedFromStaging] = 1
                      WITH (ONLINE = ON)'
                  END
                  ELSE
                  BEGIN
                      CREATE NONCLUSTERED INDEX [IX_Packages_PublishedFromStaging]
                      ON [dbo].[Packages] ([PackageRegistrationKey])
                      WHERE [PublishedFromStaging] = 1
                  END");

            Sql(@"IF SERVERPROPERTY('edition') = 'SQL Azure'
                  BEGIN
                      EXECUTE sp_executesql N'CREATE NONCLUSTERED INDEX [IX_SymbolPackages_PublishedFromStaging]
                      ON [dbo].[SymbolPackages] ([PackageKey])
                      WHERE [PublishedFromStaging] = 1
                      WITH (ONLINE = ON)'
                  END
                  ELSE
                  BEGIN
                      CREATE NONCLUSTERED INDEX [IX_SymbolPackages_PublishedFromStaging]
                      ON [dbo].[SymbolPackages] ([PackageKey])
                      WHERE [PublishedFromStaging] = 1
                  END");
        }
        
        public override void Down()
        {
            DropIndex(table: "dbo.SymbolPackages", name: "IX_SymbolPackages_PublishedFromStaging");
            DropIndex(table: "dbo.Packages", name: "IX_Packages_PublishedFromStaging");

            DropColumn("dbo.SymbolPackages", "PublishedFromStaging");
            DropColumn("dbo.Packages", "PublishedFromStaging");
        }
    }
}
