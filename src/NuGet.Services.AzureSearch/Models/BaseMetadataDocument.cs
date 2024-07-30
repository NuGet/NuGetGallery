// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure.Search.Documents.Indexes;

namespace NuGet.Services.AzureSearch
{
    public class BaseMetadataDocument : CommittedDocument, IBaseMetadataDocument
    {
        [SimpleField(IsFilterable = true)]
        public int? SemVerLevel { get; set; }

        [SearchableField(AnalyzerName = DescriptionAnalyzer.Name)]
        public string Authors { get; set; }

        public string Copyright { get; set; }

        [SimpleField(IsSortable = true)]
        public DateTimeOffset? Created { get; set; }

        [SearchableField(AnalyzerName = DescriptionAnalyzer.Name)]
        public string Description { get; set; }

        public long? FileSize { get; set; }
        public string FlattenedDependencies { get; set; }
        public string Hash { get; set; }
        public string HashAlgorithm { get; set; }
        public string IconUrl { get; set; }
        public string Language { get; set; }

        [SimpleField(IsSortable = true)]
        public DateTimeOffset? LastEdited { get; set; }

        public string LicenseUrl { get; set; }
        public string MinClientVersion { get; set; }

        [SearchableField(AnalyzerName = ExactMatchCustomAnalyzer.Name)]
        public string NormalizedVersion { get; set; }

        public string OriginalVersion { get; set; }

        /// <summary>
        /// The package's identifier. Supports case insensitive exact matching.
        /// </summary>
        [SearchableField(AnalyzerName = ExactMatchCustomAnalyzer.Name)]
        public string PackageId { get; set; }

        [SimpleField(IsFilterable = true)]
        public bool? Prerelease { get; set; }

        public string ProjectUrl { get; set; }

        [SimpleField(IsSortable = true, IsFilterable = true)]
        public DateTimeOffset? Published { get; set; }

        public string ReleaseNotes { get; set; }
        public bool? RequiresLicenseAcceptance { get; set; }

        [SimpleField(IsSortable = true)]
        public string SortableTitle { get; set; }

        [SearchableField(AnalyzerName = DescriptionAnalyzer.Name)]
        public string Summary { get; set; }

        [SearchableField(AnalyzerName = TagsCustomAnalyzer.Name)]
        public string[] Tags { get; set; }

        [SearchableField(AnalyzerName = DescriptionAnalyzer.Name)]
        public string Title { get; set; }

        /// <summary>
        /// The package's identifier. Supports tokenized search.
        /// </summary>
        [SearchableField(AnalyzerName = PackageIdCustomAnalyzer.Name)]
        public string TokenizedPackageId { get; set; }
    }
}
