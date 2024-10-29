// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.SqlServer;
#if NETFRAMEWORK
using System.Runtime.Remoting.Messaging;
#endif

namespace NuGetGallery
{
    public class EntitiesConfiguration
        : DbConfiguration
    {
#if NETFRAMEWORK
        public EntitiesConfiguration()
        {
            // Configure Connection Resiliency / Retry Logic
            // See https://msdn.microsoft.com/en-us/data/dn456835.aspx and msdn.microsoft.com/en-us/data/dn307226
            SetExecutionStrategy("System.Data.SqlClient", () => SuspendExecutionStrategy
                ? (IDbExecutionStrategy)new DefaultExecutionStrategy() : new SqlAzureExecutionStrategy());
        }

        public static bool SuspendExecutionStrategy
        {
            get
            {
                return (bool?)CallContext.LogicalGetData("SuspendExecutionStrategy") ?? false;
            }
            set
            {
                CallContext.LogicalSetData("SuspendExecutionStrategy", value);
            }
        }
#else
        public EntitiesConfiguration()
        {
            // Configure Connection Resiliency / Retry Logic
            // See https://msdn.microsoft.com/en-us/data/dn456835.aspx and msdn.microsoft.com/en-us/data/dn307226
            SetExecutionStrategy("System.Data.SqlClient", () => new SqlAzureExecutionStrategy());
        }
#endif
    }
}