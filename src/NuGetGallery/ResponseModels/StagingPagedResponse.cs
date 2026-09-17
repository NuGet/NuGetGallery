// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGetGallery
{
    /// <summary>
    /// Represents one page of staging API resources.
    /// </summary>
    public class StagingPagedResponse<T>
    {
        public StagingPagedResponse(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
        {
            Items = items;
            Page = page;
            PageSize = pageSize;
            TotalCount = totalCount;
        }

        [JsonProperty("items")]
        public IReadOnlyList<T> Items { get; }

        [JsonProperty("page")]
        public int Page { get; }

        [JsonProperty("pageSize")]
        public int PageSize { get; }

        [JsonProperty("totalCount")]
        public int TotalCount { get; }
    }

    /// <summary>
    /// Represents a staging group and one page of its members.
    /// </summary>
    public class StagingGroupDetailResponse : StagingPagedResponse<StagingArtifactResponse>
    {
        public StagingGroupDetailResponse(
            StagingGroupResponse group,
            IReadOnlyList<StagingArtifactResponse> items,
            int page,
            int pageSize,
            int totalCount)
            : base(items, page, pageSize, totalCount)
        {
            Group = group;
        }

        [JsonProperty("group")]
        public StagingGroupResponse Group { get; }
    }
}
