namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class PackageStatistics : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.PackageStatistics", "Operation", c => c.String(maxLength: 128));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.PackageStatistics", "Operation", c => c.String(maxLength: 18));
        }
    }
}
