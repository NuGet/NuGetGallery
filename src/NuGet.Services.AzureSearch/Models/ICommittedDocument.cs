// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// A document that has data committed from the catalog.
    /// </summary>
    public interface ICommittedDocument : IKeyedDocument
    {
        DateTimeOffset? LastUpdatedDocument { get; set; }
        string LastDocumentType { get; set; }
        bool? LastUpdatedFromCatalog { get; set; }
        DateTimeOffset? LastCommitTimestamp { get; set; }
        string LastCommitId { get; set; }
        void SetLastUpdatedDocumentOnNextRead();
    }
}