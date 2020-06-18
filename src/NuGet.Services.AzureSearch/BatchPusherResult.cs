// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Services.AzureSearch
{
    public class BatchPusherResult
    {
        public BatchPusherResult() : this(new HashSet<string>())
        {
        }

        public BatchPusherResult(HashSet<string> failedPackageIds)
        {
            FailedPackageIds = failedPackageIds;
        }

        /// <summary>
        /// Package IDs that failed due to access condition on the version list.
        /// </summary>
        public HashSet<string> FailedPackageIds { get; }

        public void EnsureNoFailures()
        {
            if (FailedPackageIds.Any())
            {
                throw new InvalidOperationException(
                    "The index operations for the following package IDs failed due to version list concurrency: "
                    + string.Join(", ", FailedPackageIds));
            }
        }
    }
}