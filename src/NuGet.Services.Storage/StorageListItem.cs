// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Storage
{
    public class StorageListItem
    {
        public Uri Uri { get; private set; }

        public DateTime? LastModifiedUtc { get; private set; }

        public StorageListItem(Uri uri, DateTime? lastModifiedUtc)
        {
            Uri = uri;
            LastModifiedUtc = lastModifiedUtc;
        }
    }
}
