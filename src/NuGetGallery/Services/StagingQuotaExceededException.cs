// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    /// <summary>
    /// Signals that a new private artifact would exceed its staging owner's quota.
    /// </summary>
    public class StagingQuotaExceededException : InvalidOperationException
    {
        public StagingQuotaExceededException() : base("The staging owner has reached its artifact limit. Delete staged content before uploading more.")
        {
        }
    }
}
