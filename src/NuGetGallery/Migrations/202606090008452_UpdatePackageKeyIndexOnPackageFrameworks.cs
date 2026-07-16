namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class UpdatePackageKeyIndexOnPackageFrameworks : DbMigration
    {
        public override void Up()
        {
            DropIndex(table: "dbo.PackageFrameworks", name: "IX_Package_Key");

            Sql(@"IF SERVERPROPERTY('edition') = 'SQL Azure'
                  BEGIN
                  EXECUTE sp_executesql N'CREATE NONCLUSTERED INDEX [IX_Package_Key]
                                          ON [dbo].[PackageFrameworks] ([Package_Key])
                                          INCLUDE ([TargetFramework])
                                          WITH (ONLINE = ON)'
                  END
                  ELSE
                  BEGIN
                      CREATE NONCLUSTERED INDEX [IX_Package_Key]
                      ON [dbo].[PackageFrameworks] ([Package_Key])
	                  INCLUDE ([TargetFramework])
                  END");
        }

        public override void Down()
        {
            DropIndex(table: "dbo.PackageFrameworks", name: "IX_Package_Key");

            Sql(@"IF SERVERPROPERTY('edition') = 'SQL Azure'
                  BEGIN
                  EXECUTE sp_executesql N'CREATE NONCLUSTERED INDEX [IX_Package_Key]
                                          ON [dbo].[PackageFrameworks] ([Package_Key])
                                          WITH (ONLINE = ON)'
                  END
                  ELSE
                  BEGIN
                      CREATE NONCLUSTERED INDEX [IX_Package_Key]
                      ON [dbo].[PackageFrameworks] ([Package_Key])
                  END");
        }
    }
}
