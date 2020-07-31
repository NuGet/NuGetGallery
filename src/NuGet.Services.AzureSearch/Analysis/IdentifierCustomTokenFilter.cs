// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Splits tokens on camel casing and non alpha-numeric characters.
    /// This does not consume the original token. For example, "Foo2Bar.Baz"
    /// becomes "Foo", "2", "Bar", "Baz", and "Foo2Bar.Baz".
    /// </summary>
    public static class IdentifierCustomTokenFilter
    {
        public const string Name = "nuget_id_filter";

        public static WordDelimiterTokenFilter Instance = new WordDelimiterTokenFilter(
            Name,
            splitOnCaseChange: true,
            preserveOriginal: true);
    }
}
