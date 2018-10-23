// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Search;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Implements most of <see cref="IBaseMetadataDocument"/>. Some fields are not defined here since they have
    /// different Azure Search attributes.
    /// </summary>
    public abstract class BaseMetadataDocument : KeyedDocument
    {
        [IsFilterable]
        public int? SemVerLevel { get; set; }

        public string Authors { get; set; }
        public string Copyright { get; set; }
        public DateTimeOffset? Created { get; set; }
        public string Description { get; set; }
        public long? FileSize { get; set; }
        public string FlattenedDependencies { get; set; }
        public string Hash { get; set; }
        public string HashAlgorithm { get; set; }
        public string IconUrl { get; set; }
        public string Language { get; set; }
        public string LicenseUrl { get; set; }
        public string MinClientVersion { get; set; }
        public string NormalizedVersion { get; set; }
        public string OriginalVersion { get; set; }
        public string PackageId { get; set; }
        public bool? Prerelease { get; set; }
        public string ProjectUrl { get; set; }
        public string ReleaseNotes { get; set; }
        public bool? RequiresLicenseAcceptance { get; set; }
        public string Summary { get; set; }
        public string[] Tags { get; set; }
        public string Title { get; set; }
    }
}
