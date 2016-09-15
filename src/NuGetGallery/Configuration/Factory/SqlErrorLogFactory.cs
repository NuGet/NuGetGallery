// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Elmah;

namespace NuGetGallery.Configuration.Factory
{
    public class SqlErrorLogFactory : ConfigObjectFactory<SqlErrorLog>
    {
        public SqlErrorLogFactory()
            : base(new ConfigObjectDelegate<SqlErrorLog>(
                objects => new SqlErrorLog((string)objects[0]), "SqlConnectionString"))
        {
        }
    }
}