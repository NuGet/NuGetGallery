namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    /// <summary>
    /// Records the expiration deadline already warned about without adding notification workflow tables.
    /// </summary>
    public partial class AddStagingExpirationWarnings : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.StagedPackages", "WarnedExpirationDate", c => c.DateTime(precision: 7, storeType: "datetime2"));
            AddColumn("dbo.StagedSymbolPackages", "WarnedExpirationDate", c => c.DateTime(precision: 7, storeType: "datetime2"));
            AddColumn("dbo.StagingGroups", "WarnedExpirationDate", c => c.DateTime(precision: 7, storeType: "datetime2"));
        }
        
        public override void Down()
        {
            DropColumn("dbo.StagingGroups", "WarnedExpirationDate");
            DropColumn("dbo.StagedSymbolPackages", "WarnedExpirationDate");
            DropColumn("dbo.StagedPackages", "WarnedExpirationDate");
        }
    }
}
