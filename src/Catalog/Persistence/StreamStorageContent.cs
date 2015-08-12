// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.IO;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class StreamStorageContent : StorageContent
    {
        public StreamStorageContent(Stream content, string contentType = "", string cacheControl = "")
        {
            Content = content;
            ContentType = contentType;
            CacheControl = cacheControl;
        }

        public Stream Content
        {
            get;
            set;
        }

        public override Stream GetContentStream()
        {
            return Content;
        }
    }
}
