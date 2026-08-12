// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGetGallery
{
    /// <summary>
    /// The result of an owner-authorized, Gallery-mediated staged artifact download. The service opens the exact
    /// staged blob and hands the caller a readable stream; the private blob path, ETag, and storage location are
    /// never exposed through the contract.
    /// </summary>
    public sealed class StagingDownloadResult
    {
        public StagingDownloadResult(Stream content, string fileName, string contentType)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
        }

        /// <summary>
        /// The staged artifact content. The consumer is responsible for disposing this stream.
        /// </summary>
        public Stream Content { get; }
        public string FileName { get; }
        public string ContentType { get; }
    }
}
