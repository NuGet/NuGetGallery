// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.SqlServer;
using System.Runtime.Remoting.Messaging;

namespace NuGet.Services.Validation
{
    public class EntitiesConfiguration : DbConfiguration
    {
        public EntitiesConfiguration()
        {
            // Configure Connection Resiliency / Retry Logic
            // See https://msdn.microsoft.com/en-us/data/dn456835.aspx and https://msdn.microsoft.com/en-us/data/dn307226
            SetExecutionStrategy(
                "System.Data.SqlClient",
                () => SuspendExecutionStrategy ? (IDbExecutionStrategy)new DefaultExecutionStrategy() : new SqlAzureExecutionStrategy());
        }

        public static bool SuspendExecutionStrategy
        {
            get => (bool?)CallContext.LogicalGetData("SuspendExecutionStrategy") ?? false;
            set => CallContext.LogicalSetData("SuspendExecutionStrategy", value);
        }
    }
}
