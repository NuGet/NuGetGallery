// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.SqlServer;
using System.Runtime.Remoting.Messaging;

namespace NuGetGallery
{
    public class EntitiesConfiguration
        : DbConfiguration
    {
        public EntitiesConfiguration()
        {
            // Configure Connection Resiliency / Retry Logic
            // See https://msdn.microsoft.com/en-us/data/dn456835.aspx and msdn.microsoft.com/en-us/data/dn307226
            SetExecutionStrategy("System.Data.SqlClient", () => UseRetriableExecutionStrategy
                ? new SqlAzureExecutionStrategy() : (IDbExecutionStrategy)new DefaultExecutionStrategy());
        }

        private static bool UseRetriableExecutionStrategy
        {
            get
            {
                return (bool?)CallContext.LogicalGetData("UseRetriableExecutionStrategy") ?? false;
            }
            set
            {
                CallContext.LogicalSetData("UseRetriableExecutionStrategy", value);
            }
        }

        public static IDisposable SuspendRetriableExecutionStrategy()
        {
            return new ExecutionStrategySuspension();
        }

        private class ExecutionStrategySuspension : IDisposable
        {
            private readonly bool _originalValue;

            internal ExecutionStrategySuspension()
            {
                _originalValue = UseRetriableExecutionStrategy;

                UseRetriableExecutionStrategy = false;
            }

            public void Dispose()
            {
                UseRetriableExecutionStrategy = _originalValue;
            }
        }
    }
}