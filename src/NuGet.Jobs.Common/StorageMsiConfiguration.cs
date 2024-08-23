// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs
{
    public class StorageMsiConfiguration
    {
        public bool UseManagedIdentity { get; set; }
        public string ManagedIdentityClientId { get; set; }
    }
}
