// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Azure.Search.Documents.Models;

namespace NuGet.Services.AzureSearch.Wrappers
{
    public class IndexBatchException : Exception
    {
        public IndexBatchException(IReadOnlyList<IndexingResult> indexingResults)
        {
            IndexingResults = indexingResults;
        }

        public IReadOnlyList<IndexingResult> IndexingResults { get; }
    }
}
