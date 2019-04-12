namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RemoveCuratedFeedEntities : DbMigration
    {
        public override void Up()
        {
            /*
            We intentionally do not delete the curated feeds tables with a migration. This migration simply removes the
            curated feeds from the entity model. You can use the following script to delete the tables and data
            manually:

                IF object_id(N'[dbo].[FK_dbo.CuratedFeedManagers_dbo.CuratedFeeds_CuratedFeedKey]', N'F') IS NOT NULL
                    ALTER TABLE [dbo].[CuratedFeedManagers] DROP CONSTRAINT [FK_dbo.CuratedFeedManagers_dbo.CuratedFeeds_CuratedFeedKey]
                IF object_id(N'[dbo].[FK_dbo.CuratedFeedManagers_dbo.Users_UserKey]', N'F') IS NOT NULL
                    ALTER TABLE [dbo].[CuratedFeedManagers] DROP CONSTRAINT [FK_dbo.CuratedFeedManagers_dbo.Users_UserKey]
                IF object_id(N'[dbo].[FK_dbo.CuratedPackages_dbo.PackageRegistrations_PackageRegistrationKey]', N'F') IS NOT NULL
                    ALTER TABLE [dbo].[CuratedPackages] DROP CONSTRAINT [FK_dbo.CuratedPackages_dbo.PackageRegistrations_PackageRegistrationKey]
                IF object_id(N'[dbo].[FK_dbo.CuratedPackages_dbo.CuratedFeeds_CuratedFeedKey]', N'F') IS NOT NULL
                    ALTER TABLE [dbo].[CuratedPackages] DROP CONSTRAINT [FK_dbo.CuratedPackages_dbo.CuratedFeeds_CuratedFeedKey]
                IF EXISTS (SELECT name FROM sys.indexes WHERE name = N'IX_CuratedFeedKey' AND object_id = object_id(N'[dbo].[CuratedPackages]', N'U'))
                    DROP INDEX [IX_CuratedFeedKey] ON [dbo].[CuratedPackages]
                IF EXISTS (SELECT name FROM sys.indexes WHERE name = N'IX_PackageRegistrationKey' AND object_id = object_id(N'[dbo].[CuratedPackages]', N'U'))
                    DROP INDEX [IX_PackageRegistrationKey] ON [dbo].[CuratedPackages]
                IF EXISTS (SELECT name FROM sys.indexes WHERE name = N'IX_CuratedFeedKey' AND object_id = object_id(N'[dbo].[CuratedFeedManagers]', N'U'))
                    DROP INDEX [IX_CuratedFeedKey] ON [dbo].[CuratedFeedManagers]
                IF EXISTS (SELECT name FROM sys.indexes WHERE name = N'IX_UserKey' AND object_id = object_id(N'[dbo].[CuratedFeedManagers]', N'U'))
                    DROP INDEX [IX_UserKey] ON [dbo].[CuratedFeedManagers]
                DROP TABLE [dbo].[CuratedPackages]
                DROP TABLE [dbo].[CuratedFeedManagers]
                DROP TABLE [dbo].[CuratedFeeds]

            */
        }

        public override void Down()
        {
        }
    }
}
