namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddPackageRename : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.PackageRenames",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        FromPackageRegistrationKey = c.Int(nullable: false),
                        ToPackageRegistrationKey = c.Int(nullable: false),
                        TransferPopularity = c.Boolean(nullable: false),
                        UpdatedOn = c.DateTime(nullable: false, defaultValueSql: "GETUTCDATE()"),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.PackageRegistrations", t => t.FromPackageRegistrationKey, cascadeDelete: true)
                .ForeignKey("dbo.PackageRegistrations", t => t.ToPackageRegistrationKey)
                .Index(t => new { t.FromPackageRegistrationKey, t.ToPackageRegistrationKey }, unique: true)
                .Index(t => t.TransferPopularity);
            
            AddColumn("dbo.PackageRegistrations", "RenamedMessage", c => c.String());
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.PackageRenames", "ToPackageRegistrationKey", "dbo.PackageRegistrations");
            DropForeignKey("dbo.PackageRenames", "FromPackageRegistrationKey", "dbo.PackageRegistrations");
            DropIndex("dbo.PackageRenames", new[] { "TransferPopularity" });
            DropIndex("dbo.PackageRenames", new[] { "FromPackageRegistrationKey", "ToPackageRegistrationKey" });
            DropColumn("dbo.PackageRegistrations", "RenamedMessage");
            DropTable("dbo.PackageRenames");
        }
    }
}
