// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
    /// Represents one page of staged artifacts and the owner's total private artifact usage.
    /// </summary>
    public class StagingArtifactPagedResponse : StagingPagedResponse<StagingArtifactResponse>
    {
        /// <summary>
        /// Initializes an artifact inventory response.
        /// </summary>
        /// <param name="items">The artifacts on the requested page.</param>
        /// <param name="page">The one-based page number.</param>
        /// <param name="pageSize">The number of artifacts per page.</param>
        /// <param name="totalCount">The total number of matching artifacts.</param>
        /// <param name="quota">The owner's total staging usage and effective limit.</param>
        public StagingArtifactPagedResponse(
            IReadOnlyList<StagingArtifactResponse> items, int page, int pageSize, int totalCount, StagingQuotaUsage quota)
            : base(items ?? throw new ArgumentNullException(nameof(items)), page, pageSize, totalCount)
        {
            Quota = new StagingQuotaResponse(quota);
        }

        /// <summary>
        /// Gets owner-wide usage, independent of this artifact kind, credential pattern, or page.
        /// </summary>
        [JsonProperty("quota")]
        public StagingQuotaResponse Quota { get; }
    }

    /// <summary>
    /// Reports total private artifact usage and the owner's effective limit.
    /// </summary>
    public class StagingQuotaResponse
    {
        /// <summary>
        /// Initializes public quota metadata from authoritative owner usage.
        /// </summary>
        /// <param name="usage">The owner's staging usage.</param>
        public StagingQuotaResponse(StagingQuotaUsage usage)
        {
            if (usage == null)
            {
                throw new ArgumentNullException(nameof(usage));
            }

            UsedArtifacts = usage.UsedArtifacts;
            Limit = usage.Limit;
        }

        /// <summary>
        /// Gets the number of current private packages and symbol packages.
        /// </summary>
        [JsonProperty("usedArtifacts")]
        public int UsedArtifacts { get; }

        /// <summary>
        /// Gets the owner's effective artifact limit.
        /// </summary>
        [JsonProperty("limit")]
        public int Limit { get; }
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
