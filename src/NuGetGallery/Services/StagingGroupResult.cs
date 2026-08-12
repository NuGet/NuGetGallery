// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    /// <summary>
    /// The result of a group create request. <see cref="Created"/> distinguishes a newly created group (201) from an
    /// idempotent re-create of an already existing group with identical inputs (200).
    /// </summary>
    public sealed class StagingGroupResult
    {
        public StagingGroupResult(StagingGroupResource group, bool created)
        {
            Group = group ?? throw new ArgumentNullException(nameof(group));
            Created = created;
        }

        public StagingGroupResource Group { get; }
        public bool Created { get; }
    }
}
