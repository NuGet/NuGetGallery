// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using NuGetGallery.Migrations;

namespace NuGetGallery.Infrastructure
{
    /// <summary>
    /// Gateway for accessing gallery services from outside the web environment
    /// </summary>
    /// <remarks>
    /// This is created, usually via reflection, by external consumers of the gallery services (i.e. galops). By using the gateway instead of directly
    /// constructing Entity Contexts and Migrators in the external consumers we gain a few advantages: 1) We limit the amount of reflection needed (consumers
    /// just need to dynamically create a GalleryGateway, instead of a MigrationsConfiguration, EntitiesContext (using the right constructor), etc.). 2)
    /// We abstract the consumer from the details of how the data layer gets constructed.
    /// </remarks>
    public class GalleryGateway
    {
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification="This is designed to be called using C#'s dynamic feature, so an instance method is preferable")]
        public DbMigrator CreateMigrator(string connectionString, string providerType)
        {
            var config = new MigrationsConfiguration()
            {
                TargetDatabase = new DbConnectionInfo(connectionString, providerType),
                ContextType = typeof(EntitiesContext),
                MigrationsAssembly = Assembly.Load("NuGetGallery"),
            };
            EntitiesContextFactory.OverrideConnectionString = connectionString;
            return new DbMigrator(config);
        }
    }
}
