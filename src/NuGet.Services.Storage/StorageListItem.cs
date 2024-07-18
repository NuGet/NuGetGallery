// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Storage
{
    public class StorageListItem
    {
        public Uri Uri { get; private set; }

        public DateTimeOffset? LastModifiedUtc { get; private set; }

        public IDictionary<string, string> Metadata { get; private set; }

        public StorageListItem(Uri uri, DateTimeOffset? lastModifiedUtc, IDictionary<string, string> metadata = null)
        {
            Uri = uri;
            LastModifiedUtc = lastModifiedUtc;
            Metadata = metadata;
        }
    }
}
