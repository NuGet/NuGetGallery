namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class EntityFramework6 : DbMigration
    {
        public override void Up()
        {
            // this migration is empty - it exists to make EF6 happy when running update-database
            // if we did not have this migration, EF would want to create ~40 indexes that already exist,
            // essentially breaking the database migrations feature
            // this empty migration ensures EF does not attempt to make that change
        }
        
        public override void Down()
        {
        }
    }
}
