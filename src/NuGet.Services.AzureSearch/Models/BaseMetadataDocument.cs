// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Search;
using Newtonsoft.Json;

namespace NuGet.Services.AzureSearch
{
    public abstract class BaseMetadataDocument : CommittedDocument, IBaseMetadataDocument
    {
        [IsFilterable]
        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public int? SemVerLevel { get; set; }

        [IsSearchable]
        public string Authors { get; set; }

        public string Copyright { get; set; }
        public DateTimeOffset? Created { get; set; }

        [IsSearchable]
        public string Description { get; set; }

        public long? FileSize { get; set; }
        public string FlattenedDependencies { get; set; }
        public string Hash { get; set; }
        public string HashAlgorithm { get; set; }
        public string IconUrl { get; set; }
        public string Language { get; set; }

        [IsSortable]
        public DateTimeOffset? LastEdited { get; set; }

        public string LicenseUrl { get; set; }
        public string MinClientVersion { get; set; }

        [IsSearchable]
        public string NormalizedVersion { get; set; }

        public string OriginalVersion { get; set; }

        [IsSearchable]
        public string PackageId { get; set; }

        [IsFilterable]
        public bool? Prerelease { get; set; }

        public string ProjectUrl { get; set; }

        [IsSortable]
        public DateTimeOffset? Published { get; set; }

        public string ReleaseNotes { get; set; }
        public bool? RequiresLicenseAcceptance { get; set; }

        [IsSortable]
        public string SortableTitle { get; set; }

        [IsSearchable]
        public string Summary { get; set; }

        [IsSearchable]
        public string[] Tags { get; set; }

        [IsSearchable]
        public string Title { get; set; }
    }
}
