// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery;

namespace NuGet.Jobs.Validation.Storage
{
    public interface ISimpleCloudBlobProvider
    {
        /// <summary>
        /// Initializes a blob from a blob URL. This blob URL can contain a SAS token as the query string to allow
        /// read access to private blob or allow write operations.
        /// </summary>
        /// <param name="blobUrl">The blob URL, optionally having a SAS token query string.</param>
        /// <returns>The blob.</returns>
        ISimpleCloudBlob GetBlobFromUrl(string blobUrl);
    }
}