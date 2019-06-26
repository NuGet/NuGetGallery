// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json.Serialization;

namespace NuGet.Services.AzureSearch.SearchService
{
    public static class IndexFields
    {
        private static readonly NamingStrategy CamelCaseNamingStrategy = new CamelCaseNamingStrategy();

        private static string Name(string input)
        {
            return CamelCaseNamingStrategy.GetPropertyName(input, hasSpecifiedName: false);
        }

        public static readonly string Authors = Name(nameof(BaseMetadataDocument.Authors));
        public static readonly string Created = Name(nameof(BaseMetadataDocument.Created));
        public static readonly string Description = Name(nameof(BaseMetadataDocument.Description));
        public static readonly string LastEdited = Name(nameof(BaseMetadataDocument.LastEdited));
        public static readonly string NormalizedVersion = Name(nameof(BaseMetadataDocument.NormalizedVersion));
        public static readonly string PackageId = Name(nameof(BaseMetadataDocument.PackageId));
        public static readonly string Published = Name(nameof(BaseMetadataDocument.Published));
        public static readonly string SemVerLevel = Name(nameof(BaseMetadataDocument.SemVerLevel));
        public static readonly string SortableTitle = Name(nameof(BaseMetadataDocument.SortableTitle));
        public static readonly string Summary = Name(nameof(BaseMetadataDocument.Summary));
        public static readonly string Tags = Name(nameof(BaseMetadataDocument.Tags));
        public static readonly string Title = Name(nameof(BaseMetadataDocument.Title));
        public static readonly string TokenizedPackageId = Name(nameof(BaseMetadataDocument.TokenizedPackageId));

        public static class Search
        {
            public static readonly string Owners = Name(nameof(SearchDocument.Full.Owners));
            public static readonly string SearchFilters = Name(nameof(SearchDocument.UpdateLatest.SearchFilters));
            public static readonly string TotalDownloadCount = Name(nameof(SearchDocument.Full.TotalDownloadCount));
            public static readonly string DownloadScore = Name(nameof(SearchDocument.Full.DownloadScore));
            public static readonly string Versions = Name(nameof(SearchDocument.UpdateLatest.Versions));
        }
    }
}
