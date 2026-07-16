namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class UpdatePackageKeyIndexOnPackageDeprecations : DbMigration
    {
        public override void Up()
        {
            DropIndex(table: "dbo.PackageDeprecations", name: "IX_PackageKey");

            Sql(@"IF SERVERPROPERTY('edition') = 'SQL Azure'
                  BEGIN
                  EXECUTE sp_executesql N'CREATE NONCLUSTERED INDEX [IX_PackageKey]
                                          ON [dbo].[PackageDeprecations] ([PackageKey])
                                          INCLUDE ([Status],
                                                   [AlternatePackageRegistrationKey],
                                                   [AlternatePackageKey],
                                                   [DeprecatedByUserKey],
                                                   [DeprecatedOn],
                                                   [CustomMessage])
                                          WITH (ONLINE = ON)'
                  END
                  ELSE
                  BEGIN
                      CREATE NONCLUSTERED INDEX [IX_PackageKey]
                      ON [dbo].[PackageDeprecations] ([PackageKey])
                      INCLUDE ([Status],
                               [AlternatePackageRegistrationKey],
                               [AlternatePackageKey],
                               [DeprecatedByUserKey],
                               [DeprecatedOn],
                               [CustomMessage])
                  END");
        }

        public override void Down()
        {
            DropIndex(table: "dbo.PackageDeprecations", name: "IX_PackageKey");

            Sql(@"IF SERVERPROPERTY('edition') = 'SQL Azure'
                  BEGIN
                  EXECUTE sp_executesql N'CREATE NONCLUSTERED INDEX [IX_PackageKey]
                                          ON [dbo].[PackageDeprecations] ([PackageKey])
                                          WITH (ONLINE = ON)'
                  END
                  ELSE
                  BEGIN
                      CREATE NONCLUSTERED INDEX [IX_PackageKey]
                      ON [dbo].[PackageDeprecations] ([PackageKey])
                  END");
        }
    }
}
