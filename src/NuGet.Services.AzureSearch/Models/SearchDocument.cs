// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
        public class Full : AddFirst
        {
            public long? TotalDownloadCount { get; set; }
        }

        /// <summary>
        /// Used when processing <see cref="SearchIndexChangeType.AddFirst"/>.
        /// </summary>
        [SerializePropertyNamesAsCamelCase]
        public class AddFirst : UpdateLatest
        {
            public string[] Owners { get; set; }
        }

        /// <summary>
        /// Used when processing <see cref="SearchIndexChangeType.UpdateLatest"/> or
        /// <see cref="SearchIndexChangeType.DowngradeLatest"/>.
        /// </summary>
        [SerializePropertyNamesAsCamelCase]
        public class UpdateLatest : BaseMetadataDocument, IVersions, IBaseMetadataDocument
        {
            public string FullVersion { get; set; }
            public DateTimeOffset? LastEdited { get; set; }
            public DateTimeOffset? Published { get; set; }
            public string[] Versions { get; set; }
        }

        /// <summary>
        /// Used when processing <see cref="SearchIndexChangeType.UpdateVersionList"/>.
        /// </summary>
        [SerializePropertyNamesAsCamelCase]
        public class UpdateVersionList : KeyedDocument, IVersions
        {
            public string[] Versions { get; set; }
        }

        /// <summary>
        /// Allows index updating code to apply a new version list to a document.
        /// </summary>
        public interface IVersions : IKeyedDocument
        {
            string[] Versions { get; set; }
        }
    }
}
