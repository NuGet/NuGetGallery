// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Elmah;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Auditing;
using NuGetGallery.Infrastructure;

namespace NuGetGallery.Configuration.Factory
{
    public static class Factories
    {
        public static ConfigObjectFactory<CloudAuditingService> AuditingService => new CloudAuditingServiceFactory();

        public static ConfigObjectFactory<EntitiesContext> EntitiesContext => new EntitiesContextFactory();

        public static ConfigObjectFactory<SqlErrorLog> SqlErrorLog => new SqlErrorLogFactory();

        public static ConfigObjectFactory<TableErrorLog> TableErrorLog => new TableErrorLogFactory();

        public static ConfigObjectFactory<SupportRequestDbContext> SupportRequestDbContext => new SupportRequestDbContextFactory();
    }
}