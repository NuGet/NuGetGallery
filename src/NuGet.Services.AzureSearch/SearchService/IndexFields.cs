// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json.Serialization;

namespace NuGet.Services.AzureSearch.SearchService
{
    public static class IndexFields
    {
        private static readonly NamingStrategy CamelCaseNamingStrategy = new CamelCaseNamingStrategy();

        public static readonly string LastEdited = CamelCaseNamingStrategy.GetPropertyName(
            nameof(BaseMetadataDocument.LastEdited),
            hasSpecifiedName: false);

        public static readonly string Published = CamelCaseNamingStrategy.GetPropertyName(
            nameof(BaseMetadataDocument.Published),
            hasSpecifiedName: false);

        public static readonly string SortableTitle = CamelCaseNamingStrategy.GetPropertyName(
            nameof(BaseMetadataDocument.SortableTitle),
            hasSpecifiedName: false);

        public static readonly string SemVerLevel = CamelCaseNamingStrategy.GetPropertyName(
            nameof(BaseMetadataDocument.SemVerLevel),
            hasSpecifiedName: false);

        public static class Search
        {
            public static readonly string SearchFilters = CamelCaseNamingStrategy.GetPropertyName(
                nameof(SearchDocument.UpdateLatest.SearchFilters),
                hasSpecifiedName: false);
        }
    }
}
