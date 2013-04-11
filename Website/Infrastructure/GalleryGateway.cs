using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Web;
using NuGetGallery.Migrations;

namespace NuGetGallery.Infrastructure
{
    /// <summary>
    /// Gateway for accessing gallery services from outside the web environment
    /// </summary>
    public class GalleryGateway
    {
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification="This is designed to be called using C#'s dynamic feature, so an instance method is preferable")]
        public DbMigrator CreateMigrator(string connectionString, string providerType)
        {
            var config = new MigrationsConfiguration()
            {
                TargetDatabase = new DbConnectionInfo(connectionString, providerType)
            };
            EntitiesContextFactory.OverrideConnectionString = connectionString;
            return new DbMigrator(config);
        }
    }
}