namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    /// <summary>
    /// Adds staging deadlines and grants existing staging an initial thirty-day lifetime.
    /// </summary>
    public partial class AddStagingExpirationDates : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.StagedPackages", "ExpirationDate", c => c.DateTime(nullable: false, precision: 7, defaultValueSql: "DATEADD(day, 30, SYSUTCDATETIME())", storeType: "datetime2"));
            AddColumn("dbo.StagedSymbolPackages", "ExpirationDate", c => c.DateTime(nullable: false, precision: 7, defaultValueSql: "DATEADD(day, 30, SYSUTCDATETIME())", storeType: "datetime2"));
            AddColumn("dbo.StagingGroups", "ExpirationDate", c => c.DateTime(nullable: false, precision: 7, defaultValueSql: "DATEADD(day, 30, SYSUTCDATETIME())", storeType: "datetime2"));
        }
        
        public override void Down()
        {
            DropColumn("dbo.StagingGroups", "ExpirationDate");
            DropColumn("dbo.StagedSymbolPackages", "ExpirationDate");
            DropColumn("dbo.StagedPackages", "ExpirationDate");
        }
    }
}
