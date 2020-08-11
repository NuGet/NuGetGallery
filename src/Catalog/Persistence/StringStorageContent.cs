// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class StringStorageContent : StorageContent
    {
        public StringStorageContent(string content, string contentType = "", string cacheControl = "")
        {
            Content = content;
            ContentType = contentType;
            CacheControl = cacheControl;
        }

        public string Content
        {
            get;
            set;
        }

        public override Stream GetContentStream()
        {
            if (Content == null)
            {
                return null;
            }
            else
            {
                Stream stream = new MemoryStream();
                StreamWriter writer = new StreamWriter(stream);
                writer.Write(Content);
                writer.Flush();
                stream.Seek(0, SeekOrigin.Begin);
                return stream;
            }
        }
    }
}
