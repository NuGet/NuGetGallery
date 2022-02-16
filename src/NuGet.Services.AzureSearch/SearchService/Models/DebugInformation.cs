// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure.Search.Documents;
using NuGet.Services.AzureSearch.Wrappers;

namespace NuGet.Services.AzureSearch.SearchService
{
    /// <summary>
    /// Note that the <c>object</c> type properties in this class are set like that purposefully. This enables
    /// System.Text.Json to dynamically serialize all properties of the type at runtime:
    /// https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-polymorphism#serialize-properties-of-derived-classes
    /// 
    /// The alternative is using a complex set of generics to express the actual type. This is not useful given this
    /// object is just for serializing some useful debug information. This model is rarely seen by users since it
    /// requires an unofficial query parameter.
    /// </summary>
    public class DebugInformation
    {
        public object SearchRequest { get; set; }
        public string IndexName { get; set; }
        public IndexOperationType IndexOperationType { get; set; }
        public string DocumentKey { get; set; }
        public SearchOptions SearchParameters { get; set; }
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
            SearchOptions parameters,
            string text,
            SingleSearchResultPage<T> result,
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
