// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Storage
{
    public class JTokenStorageContent : StorageContent
    {
        public JTokenStorageContent(JToken content, string contentType = "", string cacheControl = "")
        {
            Content = content;
            ContentType = contentType;
            CacheControl = cacheControl;
        }

        public JToken Content
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
                writer.Write(Content.ToString(Newtonsoft.Json.Formatting.None, new Newtonsoft.Json.JsonConverter[0]));
                writer.Flush();
                stream.Seek(0, SeekOrigin.Begin);
                return stream;
            }
        }
    }
}