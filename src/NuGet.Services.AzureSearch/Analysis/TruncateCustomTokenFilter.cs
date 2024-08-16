// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Search.Documents.Indexes.Models;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Truncates tokens to 300 characters or less. This is necessary as Azure Search's
    /// <see cref="TruncateTokenFilter"/> defaults to 10 characters.
    /// </summary>
    public static class TruncateCustomTokenFilter
    {
        public const string Name = "nuget_truncate_filter";

        public static TruncateTokenFilter Instance = new TruncateTokenFilter(Name)
        {
            Length = 300
        };
    }
}
