// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog
{
    public class CommitMetadata
    {
        public CommitMetadata()
        {
        }

        public CommitMetadata(DateTime? lastCreated, DateTime? lastEdited, DateTime? lastDeleted)
        {
            LastCreated = lastCreated;
            LastEdited = lastEdited;
            LastDeleted = lastDeleted;
        }

        public DateTime? LastCreated { get; set; }
        public DateTime? LastEdited { get; set; }
        public DateTime? LastDeleted { get; set; }
    }
}