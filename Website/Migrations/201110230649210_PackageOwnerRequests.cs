using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class PackageOwnerRequests : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "PackageOwnerRequests",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        PackageRegistrationKey = c.Int(nullable: false),
                        NewOwnerKey = c.Int(nullable: false),
                        RequestingOwnerKey = c.Int(nullable: false),
                        ConfirmationCode = c.String(),
                        RequestDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("PackageRegistrations", t => t.PackageRegistrationKey)
                .ForeignKey("Users", t => t.NewOwnerKey)
                .ForeignKey("Users", t => t.RequestingOwnerKey);
        }

        public override void Down()
        {
            DropForeignKey("PackageOwnerRequests", "PackageRegistrationKey", "PackageRegistrations", "Key");
            DropForeignKey("PackageOwnerRequests", "RequestingOwnerKey", "Users", "Key");
            DropForeignKey("PackageOwnerRequests", "NewOwnerKey", "Users", "Key");
            DropTable("PackageOwnerRequests");
        }
    }
}