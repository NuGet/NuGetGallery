// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Represents a staging group returned by the public API.
    /// </summary>
    public class StagingGroupResponse
    {
        private StagingGroupResponse()
        {
        }

        [JsonProperty("id")]
        public string Id { get; private set; }

        [JsonProperty("name")]
        public string Name { get; private set; }

        [JsonProperty("owner")]
        public string Owner { get; private set; }

        [JsonProperty("created")]
        public string Created { get; private set; }

        [JsonProperty("expires")]
        public string Expires { get; private set; }

        [JsonProperty("itemCount")]
        public int ItemCount { get; private set; }

        [JsonProperty("canPromote")]
        public bool CanPromote { get; private set; }

        [JsonProperty("blockers")]
        public IReadOnlyList<StagingBlockerResponse> Blockers { get; private set; }

        [JsonProperty("managementUrl")]
        public string ManagementUrl { get; private set; }

        public static StagingGroupResponse FromNewGroup(StagingGroup group, DateTime expirationDate, string managementUrl)
        {
            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            return new StagingGroupResponse
            {
                Id = group.Id,
                Name = group.Name,
                Owner = group.Owner.Username,
                Created = group.CreatedDate.ToString("O"),
                Expires = expirationDate.ToString("O"),
                ItemCount = 0,
                CanPromote = false,
                Blockers = new[]
                {
                    new StagingBlockerResponse("GroupEmpty", "The staging group does not contain any packages."),
                },
                ManagementUrl = managementUrl,
            };
        }
    }

    /// <summary>
    /// Represents a reason that a staging resource cannot be promoted.
    /// </summary>
    public class StagingBlockerResponse
    {
        public StagingBlockerResponse(string code, string message)
        {
            Code = code;
            Message = message;
        }

        [JsonProperty("code")]
        public string Code { get; }

        [JsonProperty("message")]
        public string Message { get; }
    }
}
