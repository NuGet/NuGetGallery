namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Int64PackageDownloadCount : DbMigration
    {
        public override void Up()
        {
            // Drop indices that depend on DownloadCount before we modify it.
            DropIndex(table: "PackageRegistrations", name: "IX_PackageRegistration_Id");
            DropIndex("PackageRegistrations", name: "IX_PackageRegistrations_Id_DownloadCount_Key");
            DropIndex("PackageRegistrations", name: "IX_PackageRegistration_IsVerified_DownloadCount");

            // Modify DownloadCount column to long
            AlterColumn("dbo.PackageRegistrations", "DownloadCount", c => c.Long(nullable: false));
            AlterColumn("dbo.Packages", "DownloadCount", c => c.Long(nullable: false));

            // Recreate the indices that were dropped
            Sql(@"Create Unique Index IX_PackageRegistration_Id on [dbo].[PackageRegistrations] (DownloadCount desc, Id asc) 
                         Include ([Key])");
            Sql("CREATE NONCLUSTERED INDEX [IX_PackageRegistrations_Id_DownloadCount_Key] ON [dbo].[PackageRegistrations] ([Id]) INCLUDE ([DownloadCount], [Key])");
            Sql("CREATE NONCLUSTERED INDEX [IX_PackageRegistration_IsVerified_DownloadCount] ON [dbo].[PackageRegistrations] ([IsVerified], [DownloadCount]) INCLUDE ([Id])");
        }
        
        public override void Down()
        {
            // Drop indices that depend on DownloadCount before we modify it.
            DropIndex(table: "PackageRegistrations", name: "IX_PackageRegistration_Id");
            DropIndex("PackageRegistrations", name: "IX_PackageRegistrations_Id_DownloadCount_Key");
            DropIndex("PackageRegistrations", name: "IX_PackageRegistration_IsVerified_DownloadCount");

            AlterColumn("dbo.Packages", "DownloadCount", c => c.Int(nullable: false));
            AlterColumn("dbo.PackageRegistrations", "DownloadCount", c => c.Int(nullable: false));

            // Recreate the indices that were dropped
            Sql(@"Create Unique Index IX_PackageRegistration_Id on [dbo].[PackageRegistrations] (DownloadCount desc, Id asc) 
                         Include ([Key])");
            Sql("CREATE NONCLUSTERED INDEX [IX_PackageRegistrations_Id_DownloadCount_Key] ON [dbo].[PackageRegistrations] ([Id]) INCLUDE ([DownloadCount], [Key])");
            Sql("CREATE NONCLUSTERED INDEX [IX_PackageRegistration_IsVerified_DownloadCount] ON [dbo].[PackageRegistrations] ([IsVerified], [DownloadCount]) INCLUDE ([Id])");
        }
    }
}
