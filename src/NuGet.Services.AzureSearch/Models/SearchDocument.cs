// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;

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
        [SerializePropertyNamesAsCamelCase]
        public class Full : UpdateLatest
        {
            [IsFilterable]
            public long? TotalDownloadCount { get; set; }

            [IsFilterable]
            public double? DownloadScore { get; set; }
        }

        /// <summary>
        /// Used when processing <see cref="SearchIndexChangeType.AddFirst"/>,
        /// <see cref="SearchIndexChangeType.UpdateLatest"/> or <see cref="SearchIndexChangeType.DowngradeLatest"/>.
        /// </summary>
        [SerializePropertyNamesAsCamelCase]
        public class UpdateLatest : BaseMetadataDocument, IVersions, IOwners
        {
            [IsSearchable]
            [Analyzer(ExactMatchCustomAnalyzer.Name)]
            public string[] Owners { get; set; }

            [IsFilterable]
            public string SearchFilters { get; set; }

            public string FullVersion { get; set; }
            public string[] Versions { get; set; }
            public bool? IsLatestStable { get; set; }
            public bool? IsLatest { get; set; }
        }

        /// <summary>
        /// Used when processing <see cref="SearchIndexChangeType.UpdateVersionList"/> and the owner information has
        /// been already been fetched for the purposes of <see cref="UpdateLatest"/>. Note that this model does not
        /// need any analyzer or other Azure Search attributes since it is not used for index creation. The
        /// <see cref="Full"/> and its parent classes handle this.
        /// </summary>
        [SerializePropertyNamesAsCamelCase]
        public class UpdateVersionListAndOwners : UpdateVersionList, IOwners
        {
            public string[] Owners { get; set; }
        }

        /// <summary>
        /// Used when processing <see cref="SearchIndexChangeType.UpdateVersionList"/>.
        /// </summary>
        [SerializePropertyNamesAsCamelCase]
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
        [SerializePropertyNamesAsCamelCase]
        public class UpdateOwners : CommittedDocument, IOwners
        {
            public string[] Owners { get; set; }
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
        public interface IOwners : ICommittedDocument
        {
            string[] Owners { get; set; }
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
}
