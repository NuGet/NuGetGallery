// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Services.AzureSearch
{
    public class BatchPusherResult
    {
        public BatchPusherResult() : this(Array.Empty<string>())
        {
        }

        public BatchPusherResult(IEnumerable<string> failedPackageIds)
        {
            FailedPackageIds = failedPackageIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Package IDs that failed due to access condition on the version list.
        /// </summary>
        public HashSet<string> FailedPackageIds { get; }

        public bool Success => !FailedPackageIds.Any();

        public void EnsureSuccess()
        {
            if (!Success)
            {
                throw new InvalidOperationException(
                    "The index operations for the following package IDs failed due to version list concurrency: "
                    + string.Join(", ", FailedPackageIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)));
            }
        }

        public BatchPusherResult Merge(BatchPusherResult other)
        {
            return new BatchPusherResult(FailedPackageIds
                .Concat(other.FailedPackageIds)
                .ToHashSet(StringComparer.OrdinalIgnoreCase));
        }
    }
}