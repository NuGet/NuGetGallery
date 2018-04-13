// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.V3PerPackage
{
    public class V3PerPackageConfiguration
    {
        public Uri SourceCatalogIndex { get; set; }
        public string StorageBaseAddress { get; set; }
        public string StorageAccountName { get; set; }
        public string StorageKeyValue { get; set; }
        public Uri ContentBaseAddress { get; set; }
        public string InstrumentationKey { get; set; }
    }
}
