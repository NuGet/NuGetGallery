// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    /// <summary>
    /// Used by <see cref="AzureStorage"/> to expose the ETag associated with a <see cref="Storage.LoadAsync(System.Uri, System.Threading.CancellationToken)"/> operation.
    /// </summary>
    public class StringStorageContentWithETag : StringStorageContent
    {
        public StringStorageContentWithETag(
            string content,
            string eTag,
            string contentType = "",
            string cacheControl = "")
            : base(content, contentType, cacheControl)
        {
            ETag = eTag;
        }

        public string ETag { get; }
    }
}
