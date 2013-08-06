namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    using System.Diagnostics;
    
    public partial class Contract_RemovePackageAuthorsTable : DbMigration
    {
        public override void Up()
        {
            // This is a drop Table migration! Instead of automigration, 
            // run the following SQL script manually.
            Trace.WriteLine(@"To drop the package authors table, run this SQL manually:

            ALTER TABLE [PackageAuthors] DROP CONSTRAINT [FK_PackageAuthors_Packages_PackageKey]
            DROP INDEX [PackageAuthors].[IX_PackageAuthors_PackageKey]
            DROP TABLE [PackageAuthors]
");
        }
        
        public override void Down()
        {
            // We can't recreate the table for you in a migration, so ...
            Trace.WriteLine(@"To recreate the package authors table, either restore it from backup, or run this SQL manually:

                CREATE TABLE [PackageAuthors] (
    [Key] [int] NOT NULL IDENTITY,
    [PackageKey] [int] NOT NULL,
    [Name] [nvarchar](max),
    CONSTRAINT [PK_PackageAuthors] PRIMARY KEY ([Key])
)
CREATE NONCLUSTERED INDEX [IX_PackageAuthors_PackageKey] ON [PackageAuthors] ([PackageKey]) INCLUDE ([Key],[Name])
ALTER TABLE [PackageAuthors] ADD CONSTRAINT [FK_PackageAuthors_Packages_PackageKey] FOREIGN KEY ([PackageKey]) REFERENCES [Packages] ([Key])");

            // Note, at this point whichever way you go, package authors are still missing from PackageAuthors table for packages uploaded while you had future migrations applied.
        }
    }
}
