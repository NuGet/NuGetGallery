namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class AddPackageLicenseReportSproc : SqlResourceMigration
    {
        public AddPackageLicenseReportSproc()
            : base("NuGetGallery.Infrastructure.AddPackageLicenseReport.sql")
        {
        }
    }
}