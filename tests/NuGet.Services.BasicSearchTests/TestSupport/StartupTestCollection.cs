// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGet.Services.BasicSearchTests.TestSupport
{
    [CollectionDefinition(Name)]
    public static class StartupTestCollection
    {
        public const string Name = "NuGet.Services.BasicSearch.Startup Test Collection";
    }
}