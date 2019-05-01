// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Support for NuGet style identifier analysis. Splits tokens
    /// on non alpha-numeric characters and camel casing, and lower
    /// cases tokens.
    /// </summary>
    public static class PackageIdCustomAnalyzer
    {
        public const string Name = "nuget_package_id_analyzer";

        public static readonly CustomAnalyzer Instance = new CustomAnalyzer(
            Name,
            PackageIdCustomTokenizer.Name,
            new List<TokenFilterName>
            {
                IdentifierCustomTokenFilter.Name,
                TokenFilterName.Lowercase,
                TokenFilterName.Truncate,
            });
    }
}
