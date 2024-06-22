namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddUserCertificatePatterns : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.UserCertificatePatterns",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        UserKey = c.Int(nullable: false),
                        PatternType = c.Int(nullable: false),
                        CreatedUtc = c.DateTime(nullable: false, precision: 7, storeType: "datetime2", defaultValueSql: "SYSUTCDATETIME()"),
                        Identifier = c.String(nullable: false, maxLength: 400),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Users", t => t.UserKey, cascadeDelete: true)
                .Index(t => new { t.UserKey, t.PatternType, t.Identifier }, unique: true);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.UserCertificatePatterns", "UserKey", "dbo.Users");
            DropIndex("dbo.UserCertificatePatterns", new[] { "UserKey", "PatternType", "Identifier" });
            DropTable("dbo.UserCertificatePatterns");
        }
    }
}
