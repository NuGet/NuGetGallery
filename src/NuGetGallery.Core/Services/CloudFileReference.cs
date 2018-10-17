// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.IO;

namespace NuGetGallery
{
    public class CloudFileReference : IFileReference
    {
        private Stream _stream;

        public string ContentId { get; }

        private CloudFileReference(Stream stream, string contentId)
        {
            ContentId = contentId;
            _stream = stream;
        }

        public Stream OpenRead()
        {
            return _stream;
        }

        public static CloudFileReference NotModified(string contentId)
        {
            return new CloudFileReference(null, contentId);
        }

        public static CloudFileReference Modified(Stream data, string contentId)
        {
            return new CloudFileReference(data, contentId);
        }
    }
}
