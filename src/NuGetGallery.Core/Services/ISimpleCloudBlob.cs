﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery
{
    public interface ISimpleCloudBlob
    {
        BlobProperties Properties { get; }
        Uri Uri { get; }
        string Name { get; }
        DateTime LastModifiedUtc { get; }
        string ETag { get; }

        Task DeleteIfExistsAsync();
        Task DownloadToStreamAsync(Stream target);
        Task DownloadToStreamAsync(Stream target, AccessCondition accessCondition);

        Task<bool> ExistsAsync();
        Task SetPropertiesAsync();
        Task UploadFromStreamAsync(Stream packageFile, bool overwrite);

        Task FetchAttributesAsync();

        /// <summary>
        /// Generates the shared read signature that if appended to the blob URI
        /// would allow reading the contents of the blob using the produced URI 
        /// only (without storage account credentials).
        /// </summary>
        /// <param name="endOfAccess">
        /// "End of access" timestamp. After the specified timestamp, 
        /// the returned signature becomes invalid if implementation supports it.
        /// Null for no time limit.
        /// </param>
        /// <returns>Shared access signature in form of URI query portion.</returns>
        string GetSharedReadSignature(DateTimeOffset? endOfAccess);
    }
}