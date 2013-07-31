namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class PackageMetadataEditable : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "PackageMetadatas",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        PackageKey = c.Int(nullable: false),
                        UserKey = c.Int(nullable: true),
                        Timestamp = c.DateTime(nullable: false),
                        IsCompleted = c.Boolean(nullable: false),
                        IsOriginalMetadata = c.Boolean(nullable: false),
                        TriedCount = c.Int(nullable: false),
                        Authors = c.String(),
                        Copyright = c.String(),
                        Description = c.String(),
                        Hash = c.String(),
                        HashAlgorithm = c.String(maxLength: 10),
                        IconUrl = c.String(),
                        LicenseUrl = c.String(),
                        PackageFileSize = c.Long(nullable: false),
                        ProjectUrl = c.String(),
                        ReleaseNotes = c.String(),
                        Summary = c.String(),
                        Tags = c.String(),
                        Title = c.String(),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("Packages", t => t.PackageKey, cascadeDelete: true)
                .ForeignKey("Users", t => t.UserKey, cascadeDelete: true)
                .Index(t => t.PackageKey);

            AddColumn("Packages", "MetadataKey", c => c.Int(nullable: true));
            AddForeignKey("Packages", "MetadataKey", "PackageMetadatas", "Key");
            CreateIndex("Packages", "MetadataKey");

            // Clone the current data from Packages table into PackagesMetadata
            Sql(@"
INSERT INTO [PackageMetadatas]
      (
--[Key],
      [PackageKey],
      [UserKey],
      [Timestamp],
      [IsCompleted],
      [IsOriginalMetadata],
      [TriedCount],
      [Authors],
      [Copyright],
      [Description],
      [Hash],
      [HashAlgorithm],
      [IconUrl],
      [LicenseUrl],
      [PackageFileSize],
      [ProjectUrl],
      [ReleaseNotes],
      [Summary],
      [Tags],
      [Title])
SELECT 
--    (ROW_NUMBER( ) OVER ( ORDER BY [Key] ASC )) +
--        COALESCE((SELECT MAX([Key]) FROM [PackageMetadatas]), 0), /*Key*/
    [Key] as [PackageKey], /*PackageKey*/
    NULL, /*UserKey*/
    SYSUTCDATETIME(), /*Timestamp*/
    1, /*IsCompleted*/
    1, /*IsOriginalMetadata*/
    0, /*TriedCount*/
    [FlattenedAuthors],/*Authors*/
    [Copyright],
    [Description],
    [Hash],
    [HashAlgorithm],
    [IconUrl],
    [LicenseUrl],
    [PackageFileSize],
    [ProjectUrl],
    [ReleaseNotes],
    [Summary],
    [Tags],
    [Title]
FROM [Packages] WHERE [MetadataKey] IS NULL");

            Sql(@"
UPDATE [Packages]
SET [MetadataKey] = (SELECT TOP 1 [Key] from [PackageMetadatas] WHERE [PackageMetadatas].[PackageKey] = [Packages].[Key])
WHERE [MetadataKey] IS NULL");
        }

        public override void Down()
        {
            DropIndex("PackageMetadatas", new[] { "PackageKey" });
            DropIndex("Packages", new[] { "MetadataKey" });
            DropForeignKey("PackageMetadatas", "UserKey", "Users");
            DropForeignKey("PackageMetadatas", "PackageKey", "Packages");
            DropForeignKey("Packages", "MetadataKey", "PackageMetadatas");
            DropColumn("Packages", "MetadataKey");
            DropTable("PackageMetadatas");
        }
    }
}
