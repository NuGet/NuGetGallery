// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class IndexOperation
    {
        private IndexOperation(
            IndexOperationType type,
            string documentKey,
            string searchText,
            SearchParameters searchParameters)
        {
            Type = type;
            DocumentKey = documentKey;
            SearchText = searchText;
            SearchParameters = searchParameters;
        }

        /// <summary>
        /// The type of index operation. This is used to determine which other properties are applicable.
        /// </summary>
        public IndexOperationType Type { get; }

        /// <summary>
        /// The key to look up an Azure Search document with.
        /// Used when <see cref="Type"/> is <see cref="IndexOperationType.Get"/>.
        /// </summary>
        public string DocumentKey { get; }

        /// <summary>
        /// The text to use for a search query.
        /// Used when <see cref="Type"/> is <see cref="IndexOperationType.Search"/>.
        /// </summary>
        public string SearchText { get; }

        /// <summary>
        /// The parameters to use for an Azure Search query.
        /// Used when <see cref="Type"/> is <see cref="IndexOperationType.Search"/>.
        /// </summary>
        public SearchParameters SearchParameters { get; }

        public static IndexOperation Get(string documentKey)
        {
            return new IndexOperation(
                IndexOperationType.Get,
                documentKey,
                searchText: null,
                searchParameters: null);
        }

        public static IndexOperation Search(string text, SearchParameters parameters)
        {
            return new IndexOperation(
                IndexOperationType.Search,
                documentKey: null,
                searchText: text,
                searchParameters: parameters);
        }

        public static IndexOperation Empty()
        {
            return new IndexOperation(
                IndexOperationType.Empty,
                documentKey: null,
                searchText: null,
                searchParameters: null);
        }
    }
}
