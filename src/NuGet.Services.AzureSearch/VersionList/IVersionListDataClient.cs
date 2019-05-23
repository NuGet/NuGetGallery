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
        Task ReplaceAsync(string id, VersionListData data, IAccessCondition accessCondition);
    }
}