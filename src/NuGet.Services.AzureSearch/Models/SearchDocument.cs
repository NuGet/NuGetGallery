// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Azure.Search.Documents.Indexes;
using NuGetGallery;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// The different models for reading from and writing to the search index.
    /// </summary>
    public static class SearchDocument
    {
        /// <summary>
        /// All fields available in the search index. Used for reading the index and updating the index from database,
        /// which has all fields available (as opposed to the catalog, which does not have all fields, like total
        /// download count).
        /// </summary>
        public class Full : UpdateLatest, IDownloadCount, IIsExcludedByDefault
        {
            [SimpleField(IsFilterable = true, IsSortable = true)]
            public long? TotalDownloadCount { get; set; }

            [SimpleField(IsFilterable = true)]
            public double? DownloadScore { get; set; }

            [SimpleField(IsFilterable = true)]
            public bool? IsExcludedByDefault { get; set; }
        }

        /// <summary>
        /// Used when processing <see cref="SearchIndexChangeType.AddFirst"/>,
        /// <see cref="SearchIndexChangeType.UpdateLatest"/> or <see cref="SearchIndexChangeType.DowngradeLatest"/>.
        /// </summary>
        public class UpdateLatest : BaseMetadataDocument, IVersions, IOwners
        {
            [SearchableField(AnalyzerName = ExactMatchCustomAnalyzer.Name)]
            public string[] Owners { get; set; }

            [SimpleField(IsFilterable = true)]
            public string SearchFilters { get; set; }

            [SimpleField(IsFilterable = true)]
            public string[] FilterablePackageTypes { get; set; }

            public string FullVersion { get; set; }
            public string[] Versions { get; set; }
            public string[] PackageTypes { get; set; }

            /// <summary>
            /// The list of a package's supported target framework generations. The four generations supported by
            /// the NuGet.org filtering experience will be represented by standardized shortname identifiers stored
            /// by the <see cref="AssetFrameworkHelper.FrameworkGenerationIdentifiers"/> class.
            /// eg. net, netframework
            /// </summary>
            [SimpleField(IsFilterable = true)]
            public string[] Frameworks { get; set; }

            /// <summary>
            /// The list of a package's supported target framework monikers, stored as normalized TFM strings (same
            /// as the 'short folder name'). This only refers to a package's asset frameworks, not the computed ones.
            /// eg. net5.0, net472, netcoreapp3.1, tizen40
            /// </summary>
            [SimpleField(IsFilterable = true)]
            public string[] Tfms { get; set; }

            /// <summary>
            /// The list of a package's 'computed' supported target framework generations. This is a superset of the
            /// 'Frameworks' field.
            /// eg. net, netframework
            /// </summary>
            [SimpleField(IsFilterable = true)]
            public string[] ComputedFrameworks { get; set; }

            /// <summary>
            /// The list of a package's 'computed' supported target framework monikers, stored as normalized TFM strings
            /// (same as the 'short folder name'). This is a superset of the 'Tfms' field.
            /// eg. net5.0, net472, netcoreapp3.1, tizen40
            /// </summary>
            [SimpleField(IsFilterable = true)]
            public string[] ComputedTfms { get; set; }

            public bool? IsLatestStable { get; set; }
            public bool? IsLatest { get; set; }

            public Deprecation Deprecation { get; set; }
            public List<Vulnerability> Vulnerabilities { get; set; }
        }

        /// <summary>
        /// Used when processing <see cref="SearchIndexChangeType.UpdateVersionList"/> and the owner information has
        /// been already been fetched for the purposes of <see cref="UpdateLatest"/>. Note that this model does not
        /// need any analyzer or other Azure Search attributes since it is not used for index creation. The
        /// <see cref="Full"/> and its parent classes handle this.
        /// </summary>
        public class UpdateVersionListAndOwners : UpdateVersionList, IOwners
        {
            public string[] Owners { get; set; }
        }

        /// <summary>
        /// Used when processing <see cref="SearchIndexChangeType.UpdateVersionList"/>.
        /// </summary>
        public class UpdateVersionList : CommittedDocument, IVersions
        {
            public string[] Versions { get; set; }
            public bool? IsLatestStable { get; set; }
            public bool? IsLatest { get; set; }
        }

        /// <summary>
        /// Used when updating just the owners of a document. Note that this model does not need any analyzer or
        /// other Azure Search attributes since it is not used for index creation. The <see cref="Full"/> and its
        /// parent classes handle this.
        /// </summary>
        public class UpdateOwners : UpdatedDocument, IOwners
        {
            public string[] Owners { get; set; }
        }

        /// <summary>
        /// Used when updating just the fields related to the download count of a document. Note that this model does
        /// not need any analyzer or other Azure Search attributes since it is not used for index creation. The
        /// <see cref="Full"/> and its parent classes handle this.
        /// </summary>
        public class UpdateDownloadCount : UpdatedDocument, IDownloadCount
        {
            public long? TotalDownloadCount { get; set; }
            public double? DownloadScore { get; set; }
        }

        /// <summary>
        /// Allows index updating code to apply a new version list to a document.
        /// </summary>
        public interface IVersions : ICommittedDocument
        {
            string[] Versions { get; set; }
            bool? IsLatestStable { get; set; }
            bool? IsLatest { get; set; }
        }

        /// <summary>
        /// Allows index updating code to apply a new list of owners to a document.
        /// </summary>
        public interface IOwners : IUpdatedDocument
        {
            string[] Owners { get; set; }
        }

        /// <summary>
        /// Allows index updating code to apply new download count information to a document.
        /// </summary>
        public interface IDownloadCount : IUpdatedDocument
        {
            long? TotalDownloadCount { get; set; }
            double? DownloadScore { get; set; }
        }

        /// <summary>
        /// Allows index updating code to apply default search exclusion information to a document.
        /// </summary>
        public interface IIsExcludedByDefault: IUpdatedDocument
        {
            bool? IsExcludedByDefault { get; set; }
        }

        /// <summary>
        /// The data required to populate <see cref="IVersions"/> and other <see cref="SearchDocument"/> classes.
        /// This information, as with all other types under <see cref="SearchDocument"/>, are specific to a single
        /// <see cref="SearchFilters"/>. That is, the latest version and its metadata given a filtered set of versions
        /// per package ID.
        /// </summary>
        public class LatestFlags
        {
            public LatestFlags(LatestVersionInfo latest, bool isLatestStable, bool isLatest)
            {
                LatestVersionInfo = latest;
                IsLatestStable = isLatestStable;
                IsLatest = isLatest;
            }

            public LatestVersionInfo LatestVersionInfo { get; }
            public bool IsLatestStable { get; }
            public bool IsLatest { get; }
        }
    }

    public class Deprecation
    {
        public AlternatePackage AlternatePackage { get; set; }

        public string Message { get; set; }

        public string[] Reasons { get; set; }
    }

    public class AlternatePackage
    {
        public string Id { get; set; }

        public string Range { get; set; }
    }

    public class Vulnerability
    {
        public string AdvisoryURL { get; set; }

        public int Severity { get; set; }
    }
}
