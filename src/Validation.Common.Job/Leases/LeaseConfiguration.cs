// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Validation.Leases
{
    public class LeaseConfiguration
    {
        public string ConnectionString { get; set; }
        public string ContainerName { get; set; }
        public string StoragePath { get; set; }
    }
}
