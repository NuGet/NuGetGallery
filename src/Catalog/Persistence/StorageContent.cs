// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.IO;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public abstract class StorageContent
    {
        public string ContentType
        {
            get;
            set;
        }

        public string CacheControl
        {
            get;
            set;
        }

        public abstract Stream GetContentStream();
    }
}
