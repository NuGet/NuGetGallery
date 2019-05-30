// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch.SearchService
{
    [Flags]
    public enum SearchStatusOptions
    {
        None = 0,
        Server = 1 << 0,
        AuxiliaryFiles = 1 << 1,
        AzureSearch = 1 << 2,
        All = ~None,
    }
}
