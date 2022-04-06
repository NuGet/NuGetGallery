// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Search.Documents.Indexes;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// This is a base type but can be used directly for operations that only require a document key, such as deleting
    /// a document.
    /// </summary>
    public class KeyedDocument : IKeyedDocument
    {
        /// <remarks>
        /// This field is filterable and sortable so that the index can be reliably enumerated for diagnostic purposes.
        /// </remarks>
        [SimpleField(IsKey = true, IsFilterable = true, IsSortable = true)]
        public string Key { get; set; }
    }
}
