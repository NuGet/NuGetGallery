// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch
{
    internal static class TestExtensionMethods
    {
        public static SearchIndexChangeType Delete(
            this FilteredVersionList list,
            string version)
        {
            return list.Delete(NuGetVersion.Parse(version));
        }
    }
}
