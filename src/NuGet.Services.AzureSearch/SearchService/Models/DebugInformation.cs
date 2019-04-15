// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class DebugInformation
    {
        public SearchRequest SearchRequest { get; set; }
        public string IndexName { get; set; }
        public SearchParameters SearchParameters { get; set; }
        public string SearchText { get; set; }
        public object DocumentSearchResult { get; set; }
        public TimeSpan QueryDuration { get; set; }
        public AuxiliaryFilesMetadata AuxiliaryFilesMetadata { get; set; }

        public static DebugInformation CreateOrNull<T>(
            SearchRequest request,
            string indexName,
            SearchParameters parameters,
            string text,
            DocumentSearchResult<T> result,
            TimeSpan duration,
            AuxiliaryFilesMetadata auxiliaryFilesMetadata) where T : class
        {
            if (!request.ShowDebug)
            {
                return null;
            }

            return new DebugInformation
            {
                SearchRequest = request,
                SearchParameters = parameters,
                SearchText = text,
                IndexName = indexName,
                DocumentSearchResult = result,
                QueryDuration = duration,
                AuxiliaryFilesMetadata = auxiliaryFilesMetadata,
            };
        }
    }
}
