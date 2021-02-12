namespace NuGet.Services.Validation
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddGenericValidations : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.PackageValidationResults",
                c => new
                    {
                        Key = c.Long(nullable: false, identity: true),
                        Type = c.String(nullable: false, maxLength: 128),
                        Data = c.String(nullable: false),
                        PackageValidationSetKey = c.Long(nullable: false),
                        PackageValidationKey = c.Guid(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.PackageValidations", t => t.PackageValidationKey)
                .ForeignKey("dbo.PackageValidationSets", t => t.PackageValidationSetKey, cascadeDelete: true)
                .Index(t => t.PackageValidationSetKey)
                .Index(t => t.PackageValidationKey);
            
            AddColumn("dbo.PackageValidationSets", "PackageContentType", c => c.String(maxLength: 128));
            AddColumn("dbo.PackageValidationSets", "Expiration", c => c.DateTime(precision: 7, storeType: "datetime2"));
            AddColumn("dbo.PackageValidationSets", "ValidationProperties", c => c.String());
            AlterColumn("dbo.PackageValidationSets", "PackageKey", c => c.Int());
            AlterColumn("dbo.PackageValidationSets", "PackageId", c => c.String(maxLength: 128));
            AlterColumn("dbo.PackageValidationSets", "PackageNormalizedVersion", c => c.String(maxLength: 64));
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.PackageValidationResults", "PackageValidationSetKey", "dbo.PackageValidationSets");
            DropForeignKey("dbo.PackageValidationResults", "PackageValidationKey", "dbo.PackageValidations");
            DropIndex("dbo.PackageValidationResults", new[] { "PackageValidationKey" });
            DropIndex("dbo.PackageValidationResults", new[] { "PackageValidationSetKey" });
            DropIndex("dbo.PackageValidationSets", "IX_PackageValidationSets_PackageId_PackageNormalizedVersion");
            DropIndex("dbo.PackageValidationSets", "IX_PackageValidationSets_PackageKey");
            Sql(
                @"UPDATE [dbo].[PackageValidationSets]
                SET [PackageKey] = 0, [PackageId] = '', [PackageNormalizedVersion] = ''
                WHERE [PackageKey] IS NULL AND [PackageId] IS NULL AND [PackageNormalizedVersion] IS NULL");
            AlterColumn("dbo.PackageValidationSets", "PackageNormalizedVersion", c => c.String(nullable: false, maxLength: 64));
            AlterColumn("dbo.PackageValidationSets", "PackageId", c => c.String(nullable: false, maxLength: 128));
            AlterColumn("dbo.PackageValidationSets", "PackageKey", c => c.Int(nullable: false));
            DropColumn("dbo.PackageValidationSets", "ValidationProperties");
            DropColumn("dbo.PackageValidationSets", "Expiration");
            DropColumn("dbo.PackageValidationSets", "PackageContentType");
            DropTable("dbo.PackageValidationResults");
            CreateIndex("dbo.PackageValidationSets", new[] { "PackageId", "PackageNormalizedVersion" }, name: "IX_PackageValidationSets_PackageId_PackageNormalizedVersion");
            CreateIndex("dbo.PackageValidationSets", "PackageKey", name: "IX_PackageValidationSets_PackageKey");
        }
    }
}
