namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class RemoveWorkItems : DbMigration
    {
        public override void Up()
        {
            DropTable("WorkItems");
        }
        
        public override void Down()
        {
            CreateTable(
                "WorkItems",
                c => new
                {
                    Id = c.Long(nullable: false, identity: true),
                    JobName = c.String(maxLength: 64),
                    WorkerId = c.String(maxLength: 64),
                    Started = c.DateTime(nullable: false),
                    Completed = c.DateTime(nullable: true),
                    ExceptionInfo = c.String(),
                })
                .PrimaryKey(t => t.Id);
        }
    }
}
