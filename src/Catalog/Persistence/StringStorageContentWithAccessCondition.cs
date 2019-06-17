// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    /// <summary>
    /// Allows specifying an <see cref="AccessCondition"/> for use by <see cref="AzureStorage"/> in a <see cref="Storage.SaveAsync(System.Uri, StorageContent, System.Threading.CancellationToken)"/> operation.
    /// </summary>
    public class StringStorageContentWithAccessCondition : StringStorageContent
    {
        public StringStorageContentWithAccessCondition(
            string content, 
            AccessCondition accessCondition, 
            string contentType = "", 
            string cacheControl = "")
            : base(content, contentType, cacheControl)
        {
            AccessCondition = accessCondition;
        }

        public AccessCondition AccessCondition { get; }
    }
}
