// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Search;
using Newtonsoft.Json;

namespace NuGet.Services.AzureSearch
{
    public abstract class CommittedDocument : UpdatedDocument, ICommittedDocument
    {
        [IsSortable]
        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public DateTimeOffset? LastCommitTimestamp { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public string LastCommitId { get; set; }
    }
}