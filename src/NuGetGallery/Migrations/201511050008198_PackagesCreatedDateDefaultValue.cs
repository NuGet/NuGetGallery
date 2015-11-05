namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class PackagesCreatedDateDefaultValue : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.Packages", "Created", c => c.DateTime(nullable: false, defaultValueSql: "GETUTCDATE()"));
            AlterColumn("dbo.Packages", "LastUpdated", c => c.DateTime(nullable: false, defaultValueSql: "GETUTCDATE()"));
            AlterColumn("dbo.Packages", "Published", c => c.DateTime(nullable: false, defaultValueSql: "GETUTCDATE()"));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.Packages", "Published", c => c.DateTime(nullable: false));
            AlterColumn("dbo.Packages", "LastUpdated", c => c.DateTime(nullable: false));
            AlterColumn("dbo.Packages", "Created", c => c.DateTime(nullable: false));
        }
    }
}
