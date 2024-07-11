// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;

namespace NuGet.Services.V3
{
    public static class DefaultBlobRequestOptions
    {
        public static readonly TimeSpan ServerTimeout = TimeSpan.FromMinutes(2);
        public static readonly TimeSpan MaximumExecutionTime = TimeSpan.FromMinutes(10);

        public static BlobRequestOptions Create()
        {
            return new BlobRequestOptions
            {
                ServerTimeout = TimeSpan.FromMinutes(2),
                MaximumExecutionTime = TimeSpan.FromMinutes(10),
                LocationMode = LocationMode.PrimaryThenSecondary,
                RetryPolicy = new ExponentialRetry(),
            };
        }
    }
}
