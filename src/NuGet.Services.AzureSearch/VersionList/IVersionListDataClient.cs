// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGetGallery;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// This interface allows the caller to read and write the Version List resource. This resource is an implementation
    /// detail of the Azure Search ingestion pipeline and provides an ID -> Versions mapping. The information stored for
    /// each version is enough to generate calculate what the latest version transition is per package ID, given the
    /// caller's SemVer 2.0.0 filtering preference. In other words, each version has a "SemVer2" and "Listed" boolean
    /// property. This version list should be read before pushing changes to the the Azure Search indexes and updated
    /// after. See <see cref="VersionListData"/> to see all information available per package ID.
    /// </summary>
    public interface IVersionListDataClient
    {
        Task<ResultAndAccessCondition<VersionListData>> ReadAsync(string id);

        /// <summary>
        /// Replace the version list of the provided package ID. May return false due to access condition (i.e. 412
        /// Precondition Failed in Azure Blob Storage). May throw exceptions unrelated to access condition failures.
        /// False will be returned if another caller has modified the version list thus invalidating the access
        /// condition.
        /// </summary>
        /// <param name="id">The package ID.</param>
        /// <param name="data">The data of the version list to be written.</param>
        /// <param name="accessCondition">The access condition for the write operation.</param>
        /// <returns>True if the access condition is accepted. False if the access condition fails.</returns>
        Task<bool> TryReplaceAsync(string id, VersionListData data, IAccessCondition accessCondition);
    }
}