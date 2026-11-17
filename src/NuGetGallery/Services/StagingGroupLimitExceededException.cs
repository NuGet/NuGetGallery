// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    /// <summary>
    /// Signals that a new group would exceed its staging owner's retained group limit.
    /// </summary>
    public class StagingGroupLimitExceededException : InvalidOperationException
    {
        /// <summary>
        /// Explains how an owner can make room for another group.
        /// </summary>
        public const string LimitReachedMessage = "The staging owner has reached its group limit. Delete a staging group before creating another.";

        public StagingGroupLimitExceededException() : base(LimitReachedMessage)
        {
        }
    }
}
