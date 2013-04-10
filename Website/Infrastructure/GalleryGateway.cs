using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
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