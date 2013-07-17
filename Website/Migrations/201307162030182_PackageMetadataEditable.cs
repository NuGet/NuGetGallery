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
                        Key = c.Int(nullable: false),
                        PackageKey = c.Int(nullable: false),
                        EditName = c.String(maxLength: 64),
                        Timestamp = c.DateTime(nullable: false),
                        IsCompleted = c.Boolean(nullable: false),
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
                .Index(t => t.PackageKey)
                .Index(t => t.Key);

            AddColumn("Packages", "MetadataKey", c => c.Int());
            AddForeignKey("Packages", "MetadataKey", "PackageMetadatas"); //, principalColumn: "Key"

            // Clone the current data from Packages table into PackagesMetadata
            Sql(@"
INSERT INTO [PackageMetadatas]
      ([Key],
      [PackageKey],
      [EditName],
      [Timestamp],
      [IsCompleted],
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
    (ROW_NUMBER( ) OVER ( ORDER BY [Key] ASC )) +
        COALESCE((SELECT MAX([Key]) FROM [PackageMetadatas]), 0), /*Key*/
    [Key] as [PackageKey], /*PackageKey*/
    'OriginalMetadata', /*EditName*/
    SYSUTCDATETIME(), /*Timestamp*/
    1, /*IsCompleted*/
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
FROM [Packages]");
        }
        
        public override void Down()
        {
            DropForeignKey("Packages", "MetadataKey", "PackageMetadatas"); //, principalColumn: "Key"
            DropIndex("PackageMetadatas", new[] { "Key" });
            DropIndex("PackageMetadatas", new[] { "PackageKey" });
            DropForeignKey("PackageMetadatas", "PackageKey", "Packages");
            DropColumn("Packages", "MetadataKey");
            DropTable("PackageMetadatas");
        }
    }
}
