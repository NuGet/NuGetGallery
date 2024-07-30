// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.PackageHash
{
    public class PackageSource
    {
        public string Url { get; set; }
        public PackageSourceType Type { get; set; }
    }
}
