using System;
using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class PrereleaseChanges : DbMigration
    {
        public override void Up()
        {
            AddColumn("Packages", "IsLatestStable", c => c.Boolean(nullable: false));
            AddColumn("Packages", "Listed", c => c.Boolean(nullable: false));
            AddColumn("Packages", "IsPrerelease", c => c.Boolean(nullable: false));
            AlterColumn("Packages", "Published", c => c.DateTime(nullable: false, defaultValue: DateTime.UtcNow));
            DropColumn("Packages", "IsAbsoluteLatest");
            DropColumn("Packages", "Unlisted");
        }

        public override void Down()
        {
            AddColumn("Packages", "Unlisted", c => c.Boolean(nullable: false));
            AddColumn("Packages", "IsAbsoluteLatest", c => c.Boolean(nullable: false));
            AlterColumn("Packages", "Published", c => c.DateTime());
            DropColumn("Packages", "IsPrerelease");
            DropColumn("Packages", "Listed");
            DropColumn("Packages", "IsLatestStable");
        }
    }
}