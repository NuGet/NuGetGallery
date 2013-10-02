namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class CredentialsTable : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Credentials",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        UserKey = c.Int(nullable: false),
                        Type = c.String(nullable: false, maxLength: 64),
                        Value = c.String(nullable: false, maxLength: 256),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Users", t => t.UserKey, cascadeDelete: true)
                .Index(t => t.UserKey);

            CreateIndex(
                 "dbo.Credentials",
                 new[] { "Type", "Value" },
                 unique: true,
                 name: "IX_Credentials_Type_Value");

            Sql(@"CREATE VIEW UsersAndCredentials AS
                SELECT u.Username, u.ApiKey, u.HashedPassword, c.[Type], c.Value
                FROM Users u
                    LEFT OUTER JOIN [Credentials] c ON c.UserKey = u.[Key]");
        }
        
        public override void Down()
        {
            DropIndex("dbo.Credentials", new[] { "UserKey" });
            DropIndex("dbo.Credentials", "IX_Credentials_Type_Value");
            DropForeignKey("dbo.Credentials", "UserKey", "dbo.Users");
            DropTable("dbo.Credentials");

            Sql(@"IF EXISTS(SELECT * FROM sys.views WHERE name = 'UsersAndCredentials')
                DROP VIEW UsersAndCredentials");
        }
    }
}
