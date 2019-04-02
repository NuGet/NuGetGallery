namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class FixOnDeleteSetNull : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.EmailMessages", "FromUserKey", "dbo.Users");
            DropForeignKey("dbo.EmailMessages", "ToUserKey", "dbo.Users");
            DropForeignKey("dbo.OrganizationMigrationRequests", "NewOrganizationKey", "dbo.Users");
            DropForeignKey("dbo.Scopes", "OwnerKey", "dbo.Users");
            DropForeignKey("dbo.PackageDeletes", "DeletedByKey", "dbo.Users");
            DropIndex("dbo.EmailMessages", new[] { "FromUserKey" });
            DropIndex("dbo.EmailMessages", new[] { "ToUserKey" });
            DropIndex("dbo.PackageDeletes", new[] { "DeletedByKey" });
            DropIndex("dbo.AccountDeletes", new[] { "DeletedByKey" });
            AlterColumn("dbo.PackageDeletes", "DeletedByKey", c => c.Int());
            AlterColumn("dbo.AccountDeletes", "DeletedByKey", c => c.Int());
            CreateIndex("dbo.PackageDeletes", "DeletedByKey");
            CreateIndex("dbo.AccountDeletes", "DeletedByKey");
            AddForeignKey("dbo.OrganizationMigrationRequests", "NewOrganizationKey", "dbo.Users", "Key", cascadeDelete: true);
            AddForeignKey("dbo.Scopes", "OwnerKey", "dbo.Users", "Key", cascadeDelete: true);
            AddForeignKey("dbo.PackageDeletes", "DeletedByKey", "dbo.Users", "Key");
            DropTable("dbo.EmailMessages");

            AlterForeignKeyToOnDeleteSetNull("PackageDeletes", "DeletedByKey", "Users", "Key");
            AlterForeignKeyToOnDeleteSetNull("AccountDeletes", "DeletedByKey", "Users", "Key");

            AlterForeignKeyToOnDeleteSetNull("PackageDeprecations", "AlternatePackageRegistrationKey", "PackageRegistrations", "Key");
            AlterForeignKeyToOnDeleteSetNull("PackageDeprecations", "AlternatePackageKey", "Packages", "Key");
            AlterForeignKeyToOnDeleteSetNull("PackageDeprecations", "DeprecatedByUser", "User", "Key");
        }
        
        public override void Down()
        {
            CreateTable(
                "dbo.EmailMessages",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        Body = c.String(),
                        FromUserKey = c.Int(),
                        Sent = c.Boolean(nullable: false),
                        Subject = c.String(),
                        ToUserKey = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Key);
            
            DropForeignKey("dbo.PackageDeletes", "DeletedByKey", "dbo.Users");
            DropForeignKey("dbo.Scopes", "OwnerKey", "dbo.Users");
            DropForeignKey("dbo.OrganizationMigrationRequests", "NewOrganizationKey", "dbo.Users");
            DropIndex("dbo.AccountDeletes", new[] { "DeletedByKey" });
            DropIndex("dbo.PackageDeletes", new[] { "DeletedByKey" });
            AlterColumn("dbo.AccountDeletes", "DeletedByKey", c => c.Int(nullable: false));
            AlterColumn("dbo.PackageDeletes", "DeletedByKey", c => c.Int(nullable: false));
            CreateIndex("dbo.AccountDeletes", "DeletedByKey");
            CreateIndex("dbo.PackageDeletes", "DeletedByKey");
            CreateIndex("dbo.EmailMessages", "ToUserKey");
            CreateIndex("dbo.EmailMessages", "FromUserKey");
            AddForeignKey("dbo.PackageDeletes", "DeletedByKey", "dbo.Users", "Key", cascadeDelete: true);
            AddForeignKey("dbo.Scopes", "OwnerKey", "dbo.Users", "Key");
            AddForeignKey("dbo.OrganizationMigrationRequests", "NewOrganizationKey", "dbo.Users", "Key");
            AddForeignKey("dbo.EmailMessages", "ToUserKey", "dbo.Users", "Key", cascadeDelete: true);
            AddForeignKey("dbo.EmailMessages", "FromUserKey", "dbo.Users", "Key");

            AlterForeignKeyToOnDeleteSetNoAction("PackageDeprecations", "AlternatePackageRegistrationKey", "PackageRegistrations", "Key");
            AlterForeignKeyToOnDeleteSetNoAction("PackageDeprecations", "AlternatePackageKey", "Packages", "Key");
            AlterForeignKeyToOnDeleteSetNoAction("PackageDeprecations", "DeprecatedByUser", "User", "Key");
        }

        private void AlterForeignKeyToOnDeleteSetNull(string dependentTable, string dependentColumn, string principalTable, string principalColumn)
        {
            Sql($@"
ALTER TABLE [{dependentTable}] WITH CHECK ADD CONSTRAINT [FK_{dependentTable}_{principalTable}_{dependentColumn}] FOREIGN KEY([{dependentColumn}])
REFERENCES [{principalTable}] ([{principalColumn}]) ON DELETE SET NULL");
        }

        private void AlterForeignKeyToOnDeleteSetNoAction(string dependentTable, string dependentColumn, string principalTable, string principalColumn)
        {
            Sql($@"
ALTER TABLE [{dependentTable}] WITH CHECK ADD CONSTRAINT [FK_{dependentTable}_{principalTable}_{dependentColumn}] FOREIGN KEY([{dependentColumn}])
REFERENCES [{principalTable}] ([{principalColumn}])");
        }
    }
}
