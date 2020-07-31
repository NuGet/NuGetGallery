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
        public IndexOperationType IndexOperationType { get; set; }
        public string DocumentKey { get; set; }
        public SearchParameters SearchParameters { get; set; }
        public string SearchText { get; set; }
        public object DocumentSearchResult { get; set; }
        public TimeSpan? QueryDuration { get; set; }
        public AuxiliaryFilesMetadata AuxiliaryFilesMetadata { get; set; }

        public static DebugInformation CreateFromEmptyOrNull(SearchRequest request)
        {
            if (!request.ShowDebug)
            {
                return null;
            }

            return new DebugInformation
            {
                SearchRequest = request,
                IndexOperationType = IndexOperationType.Empty,
            };
        }

        public static DebugInformation CreateFromSearchOrNull<T>(
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
                IndexName = indexName,
                IndexOperationType = IndexOperationType.Search,
                SearchParameters = parameters,
                SearchText = text,
                DocumentSearchResult = result,
                QueryDuration = duration,
                AuxiliaryFilesMetadata = auxiliaryFilesMetadata,
            };
        }

        public static DebugInformation CreateFromGetOrNull(
            SearchRequest request,
            string indexName,
            string documentKey,
            TimeSpan duration,
            AuxiliaryFilesMetadata auxiliaryFilesMetadata)
        {
            if (!request.ShowDebug)
            {
                return null;
            }

            return new DebugInformation
            {
                SearchRequest = request,
                IndexName = indexName,
                IndexOperationType = IndexOperationType.Get,
                DocumentKey = documentKey,
                QueryDuration = duration,
                AuxiliaryFilesMetadata = auxiliaryFilesMetadata,
            };
        }
    }
}
