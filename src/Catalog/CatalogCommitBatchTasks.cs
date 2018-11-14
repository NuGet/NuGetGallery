// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Metadata.Catalog
{
    /// <summary>
    /// Represents a set of <see cref="CatalogCommitBatchTask" /> that share the same minimum commit timestamp.
    /// </summary>
    public sealed class CatalogCommitBatchTasks
    {
        public CatalogCommitBatchTasks(DateTime commitTimeStamp)
        {
            BatchTasks = new HashSet<CatalogCommitBatchTask>();
            CommitTimeStamp = commitTimeStamp;
        }

        public HashSet<CatalogCommitBatchTask> BatchTasks { get; }
        public DateTime CommitTimeStamp { get; }
    }
}