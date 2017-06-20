// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    public sealed class CatalogProperties
    {
        public DateTime? LastCreated { get; }
        public DateTime? LastDeleted { get; }
        public DateTime? LastEdited { get; }

        public CatalogProperties(DateTime? lastCreated, DateTime? lastDeleted, DateTime? lastEdited)
        {
            LastCreated = lastCreated;
            LastDeleted = lastDeleted;
            LastEdited = lastEdited;
        }
    }
}