// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.AzureSearch
{
    public enum SearchIndexChangeType
    {
        /// <summary>
        /// The only change necessary is updating the version list and "is latest" flags. Some potentially no-op cases
        /// also fall into this category to support reflowing.
        /// </summary>
        UpdateVersionList,

        /// <summary>
        /// The latest version has been deleted or unlisted, so we need to replace the latest version metadata with an
        /// older version's metadata.
        /// </summary>
        DowngradeLatest,

        /// <summary>
        /// Update the latest version's metadata and version list. This represents both updating the metadata of the
        /// existing latest version and changing the latest version and metadata to a later version.
        /// </summary>
        UpdateLatest,

        /// <summary>
        /// The first listed version has been added, so we need to fetch owner information in addition to replacing
        /// metadata and version list.
        /// </summary>
        AddFirst,

        /// <summary>
        /// The last version was unlisted or deleted, so we need to delete the document from the index. Some
        /// potentially no-op cases also fall into this category to support reflowing.
        /// </summary>
        Delete,
    }
}
