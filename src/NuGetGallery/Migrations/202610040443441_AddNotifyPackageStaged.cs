namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    /// <summary>
    /// Enables optional staging notifications for existing and new active accounts.
    /// </summary>
    public partial class AddNotifyPackageStaged : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Users", "NotifyPackageStaged", c => c.Boolean(nullable: false, defaultValue: true));
            Sql("UPDATE dbo.Users SET NotifyPackageStaged = 0 WHERE IsDeleted = 1");
        }
        
        public override void Down()
        {
            DropColumn("dbo.Users", "NotifyPackageStaged");
        }
    }
}
