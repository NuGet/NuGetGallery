// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class ByteArrayStorageContent : StorageContent
    {
        public ByteArrayStorageContent(byte[] content, string contentType = "", string cacheControl = "")
        {
            Content = content;
            ContentType = contentType;
            CacheControl = cacheControl;
        }

        public byte[] Content { get; set; }

        public override Stream GetContentStream()
        {
            if (Content == null)
            {
                return null;
            }

            return new MemoryStream(Content);
        }
    }
}
