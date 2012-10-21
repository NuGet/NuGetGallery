using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class Initial : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "Users",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        ApiKey = c.Guid(nullable: false),
                        EmailAddress = c.String(),
                        UnconfirmedEmailAddress = c.String(),
                        HashedPassword = c.String(),
                        Username = c.String(),
                        EmailAllowed = c.Boolean(nullable: false),
                        EmailConfirmationToken = c.String(),
                        PasswordResetToken = c.String(),
                        PasswordResetTokenExpirationDate = c.DateTime(),
                    })
                .PrimaryKey(t => t.Key);

            CreateTable(
                "EmailMessages",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        Body = c.String(),
                        FromUserKey = c.Int(),
                        Sent = c.Boolean(nullable: false),
                        Subject = c.String(),
                        ToUserKey = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("Users", t => t.FromUserKey)
                .ForeignKey("Users", t => t.ToUserKey);

            CreateTable(
                "Roles",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        Name = c.String(),
                    })
                .PrimaryKey(t => t.Key);

            CreateTable(
                "PackageRegistrations",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        Id = c.String(),
                        DownloadCount = c.Int(nullable: false, defaultValue: 0),
                    })
                .PrimaryKey(t => t.Key);

            CreateTable(
                "Packages",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        PackageRegistrationKey = c.Int(nullable: false),
                        Copyright = c.String(),
                        Created = c.DateTime(nullable: false),
                        Description = c.String(),
                        DownloadCount = c.Int(nullable: false, defaultValue: 0),
                        ExternalPackageUrl = c.String(),
                        HashAlgorithm = c.String(),
                        Hash = c.String(),
                        IconUrl = c.String(),
                        IsLatest = c.Boolean(nullable: false),
                        IsAbsoluteLatest = c.Boolean(nullable: false),
                        LastUpdated = c.DateTime(nullable: false),
                        LicenseUrl = c.String(),
                        Published = c.DateTime(),
                        PackageFileSize = c.Long(nullable: false),
                        ProjectUrl = c.String(),
                        RequiresLicenseAcceptance = c.Boolean(nullable: false),
                        Summary = c.String(),
                        Tags = c.String(),
                        Title = c.String(),
                        Version = c.String(),
                        Unlisted = c.Boolean(nullable: false),
                        FlattenedAuthors = c.String(),
                        FlattenedDependencies = c.String(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("PackageRegistrations", t => t.PackageRegistrationKey);

            CreateTable(
                "PackageStatistics",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        PackageKey = c.Int(nullable: false),
                        Timestamp = c.DateTime(nullable: false),
                        IPAddress = c.String(),
                        UserAgent = c.String(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("Packages", t => t.PackageKey);

            CreateTable(
                "PackageAuthors",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        PackageKey = c.Int(nullable: false),
                        Name = c.String(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("Packages", t => t.PackageKey);

            CreateTable(
                "PackageDependencies",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        PackageKey = c.Int(nullable: false),
                        Id = c.String(),
                        VersionRange = c.String(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("Packages", t => t.PackageKey);

            CreateTable(
                "UserRoles",
                c => new
                    {
                        UserKey = c.Int(nullable: false),
                        RoleKey = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.UserKey, t.RoleKey })
                .ForeignKey("Users", t => t.UserKey)
                .ForeignKey("Roles", t => t.RoleKey);

            CreateTable(
                "PackageRegistrationOwners",
                c => new
                    {
                        PackageRegistrationKey = c.Int(nullable: false),
                        UserKey = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.PackageRegistrationKey, t.UserKey })
                .ForeignKey("PackageRegistrations", t => t.PackageRegistrationKey)
                .ForeignKey("Users", t => t.UserKey);
        }

        public override void Down()
        {
            DropForeignKey("PackageRegistrationOwners", "UserKey", "Users", "Key");
            DropForeignKey("PackageRegistrationOwners", "PackageRegistrationKey", "PackageRegistrations", "Key");
            DropForeignKey("UserRoles", "RoleKey", "Roles", "Key");
            DropForeignKey("UserRoles", "UserKey", "Users", "Key");
            DropForeignKey("PackageDependencies", "PackageKey", "Packages", "Key");
            DropForeignKey("PackageAuthors", "PackageKey", "Packages", "Key");
            DropForeignKey("PackageStatistics", "PackageKey", "Packages", "Key");
            DropForeignKey("Packages", "PackageRegistrationKey", "PackageRegistrations", "Key");
            DropForeignKey("EmailMessages", "ToUserKey", "Users", "Key");
            DropForeignKey("EmailMessages", "FromUserKey", "Users", "Key");
            DropTable("PackageRegistrationOwners");
            DropTable("UserRoles");
            DropTable("PackageDependencies");
            DropTable("PackageAuthors");
            DropTable("PackageStatistics");
            DropTable("Packages");
            DropTable("PackageRegistrations");
            DropTable("Roles");
            DropTable("EmailMessages");
            DropTable("Users");
        }
    }
}