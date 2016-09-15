// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Infrastructure;

namespace NuGetGallery.Configuration.Factory
{
    public class TableErrorLogFactory : ConfigObjectFactory<TableErrorLog>
    {
        public TableErrorLogFactory()
            : base(new ConfigObjectDelegate<TableErrorLog>(
                objects => new TableErrorLog((string)objects[0]), "AzureStorageConnectionString"))
        {
        }
    }
}