// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// A document that been updated.
    /// </summary>
    public interface IUpdatedDocument : IKeyedDocument
    {
        DateTimeOffset? LastUpdatedDocument { get; set; }
        string LastDocumentType { get; set; }
        bool? LastUpdatedFromCatalog { get; set; }
        void SetLastUpdatedDocumentOnNextRead();
    }
}