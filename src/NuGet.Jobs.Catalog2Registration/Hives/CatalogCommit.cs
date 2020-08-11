// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Catalog2Registration
{
    public class CatalogCommit
    {
        public CatalogCommit(string id, DateTimeOffset timestamp)
        {
            Id = id;
            Timestamp = timestamp;
        }

        public string Id { get; }
        public DateTimeOffset Timestamp { get; }
    }
}
