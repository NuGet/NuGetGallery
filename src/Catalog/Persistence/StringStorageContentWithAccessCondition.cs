// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    /// <summary>
    /// Allows specifying an <see cref="AccessConditionWrapper"/> for use by <see cref="AzureStorage"/> in a <see cref="Storage.SaveAsync(System.Uri, StorageContent, System.Threading.CancellationToken)"/> operation.
    /// </summary>
    public class StringStorageContentWithAccessConditionWrapper : StringStorageContent
    {
        public StringStorageContentWithAccessConditionWrapper(
            string content, 
            AccessConditionWrapper accessConditionWrapper, 
            string contentType = "", 
            string cacheControl = "")
            : base(content, contentType, cacheControl)
        {
            AccessConditionWrapper = accessConditionWrapper;
        }

        public AccessConditionWrapper AccessConditionWrapper { get; }
    }
}
