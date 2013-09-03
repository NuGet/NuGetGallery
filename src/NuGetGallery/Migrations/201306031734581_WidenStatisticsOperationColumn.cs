namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class WidenStatisticsOperationColumn : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.PackageStatistics", "Operation", c => c.String(maxLength: 18));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.PackageStatistics", "Operation", c => c.String(maxLength: 16));
        }
    }
}
