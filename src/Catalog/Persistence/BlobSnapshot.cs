// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class BlobSnapshot : Snapshot
    {
        public BlobSnapshot(Uri resourceUri, string identifier)
        {
            ResourceUri = resourceUri;
            Identifier = identifier;
            CreatedTimestampInUtc = DateTime.Parse(identifier, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }
    }
}
