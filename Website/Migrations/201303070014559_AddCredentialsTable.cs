namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class AddCredentialsTable : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "Credentials",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        UserKey = c.Int(nullable: false),
                        Name = c.String(),
                        Value = c.String(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("Users", t => t.UserKey, cascadeDelete: true)
                .Index(t => t.UserKey);
            
        }
        
        public override void Down()
        {
            DropIndex("Credentials", new[] { "UserKey" });
            DropForeignKey("Credentials", "UserKey", "Users");
            DropTable("Credentials");
        }
    }
}
