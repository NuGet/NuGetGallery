// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure.Search.Documents.Indexes;

namespace NuGet.Services.AzureSearch
{
    public abstract class CommittedDocument : UpdatedDocument, ICommittedDocument
    {
        [SimpleField(IsSortable = true)]
        public DateTimeOffset? LastCommitTimestamp { get; set; }

        public string LastCommitId { get; set; }
    }
}