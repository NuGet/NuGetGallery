﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json;

namespace NuGet.Services.AzureSearch.SearchService
{
    public static class IndexFields
    {
        private static string Name(string input)
        {
            return JsonNamingPolicy.CamelCase.ConvertName(input);
        }

        public static readonly string Authors = Name(nameof(BaseMetadataDocument.Authors));
        public static readonly string Created = Name(nameof(BaseMetadataDocument.Created));
        public static readonly string Description = Name(nameof(BaseMetadataDocument.Description));
        public static readonly string LastCommitTimestamp = Name(nameof(BaseMetadataDocument.LastCommitTimestamp));
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
            public static readonly string ComputedFrameworks = Name(nameof(SearchDocument.UpdateLatest.ComputedFrameworks));
            public static readonly string ComputedTfms = Name(nameof(SearchDocument.UpdateLatest.ComputedTfms));
            public static readonly string DownloadScore = Name(nameof(SearchDocument.Full.DownloadScore));
            public static readonly string FilterablePackageTypes = Name(nameof(SearchDocument.UpdateLatest.FilterablePackageTypes));
            public static readonly string Frameworks = Name(nameof(SearchDocument.UpdateLatest.Frameworks));
            public static readonly string IsExcludedByDefault = Name(nameof(SearchDocument.Full.IsExcludedByDefault));
            public static readonly string Owners = Name(nameof(SearchDocument.Full.Owners));
            public static readonly string SearchFilters = Name(nameof(SearchDocument.UpdateLatest.SearchFilters));
            public static readonly string Tfms = Name(nameof(SearchDocument.UpdateLatest.Tfms));
            public static readonly string TotalDownloadCount = Name(nameof(SearchDocument.Full.TotalDownloadCount));
            public static readonly string Versions = Name(nameof(SearchDocument.UpdateLatest.Versions));
        }
    }
}
