// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class ParsedQuery
    {
        public ParsedQuery(string text, string packageId)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            PackageId = packageId;
        }

        /// <summary>
        /// The text that will be provided to Azure Search. This is a Lucene query, not the query provided by the user.
        /// </summary>
        public string Text { get; }

        public string PackageId { get; }
    }
}
