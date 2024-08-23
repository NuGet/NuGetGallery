// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// The fields shared between the search index and hijack index.
    /// </summary>
    public interface IBaseMetadataDocument : ICommittedDocument
    {
        string Authors { get; set; }
        string Copyright { get; set; }
        DateTimeOffset? Created { get; set; }
        string Description { get; set; }
        long? FileSize { get; set; }
        string FlattenedDependencies { get; set; }
        string Hash { get; set; }
        string HashAlgorithm { get; set; }
        string IconUrl { get; set; }
        string Language { get; set; }
        DateTimeOffset? LastEdited { get; set; }
        string LicenseUrl { get; set; }
        string MinClientVersion { get; set; }
        string NormalizedVersion { get; set; }
        string OriginalVersion { get; set; }
        string PackageId { get; set; }
        string TokenizedPackageId { get; set; }
        bool? Prerelease { get; set; }
        string ProjectUrl { get; set; }
        DateTimeOffset? Published { get; set; }
        string ReleaseNotes { get; set; }
        bool? RequiresLicenseAcceptance { get; set; }
        int? SemVerLevel { get; set; }
        string SortableTitle { get; set; }
        string Summary { get; set; }
        string[] Tags { get; set; }
        string Title { get; set; }
    }
}