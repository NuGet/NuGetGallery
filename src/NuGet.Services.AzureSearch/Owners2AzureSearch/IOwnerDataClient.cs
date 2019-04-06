// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.Owners2AzureSearch
{
    /// <summary>
    /// The purpose of this interface is allow reading and writing owner information from storage. The Catalog2Owners
    /// job does a comparison of latest owner data from the database with a snapshot of information stored in Azure
    /// Blob Storage. This interface handles the reading and writing of that snapshot from storage.
    /// </summary>
    public interface IOwnerDataClient
    {
        /// <summary>
        /// Read all of the latest indexed owners from storage. Also, return the current etag to allow optimistic
        /// concurrency checks on the writing of the file. The returned dictionary has a key which is the package ID
        /// and a value which is the owners of that package ID. The dictionary and the sets are case-insensitive.
        /// </summary>
        Task<ResultAndAccessCondition<SortedDictionary<string, SortedSet<string>>>> ReadLatestIndexedAsync();

        /// <summary>
        /// Replace the existing latest indexed owners file (i.e. "owners.v2.json" file).
        /// </summary>
        /// <param name="newData">The new data to be serialized into storage.</param>
        /// <param name="accessCondition">The access condition (i.e. etag) to use during the upload.</param>
        Task ReplaceLatestIndexedAsync(
            SortedDictionary<string, SortedSet<string>> newData,
            IAccessCondition accessCondition);

        /// <summary>
        /// Write a list of owners to storage. The file name that will be used in storage will be a timestamp so
        /// subsequent calls should not conflict.
        /// </summary>
        /// <param name="packageIds">A non-empty list of package IDs that had owner changes.</param>
        Task UploadChangeHistoryAsync(IReadOnlyList<string> packageIds);
    }
}

