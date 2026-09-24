// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    /// <summary>
    /// Determines when an active promotion can be resent without creating a new attempt.
    /// </summary>
    internal static class StagingPromotionResendPolicy
    {
        private static readonly TimeSpan MinimumDelay = TimeSpan.FromHours(1);

        internal static bool IsDue(DateTime? sentDate)
        {
            return sentDate.HasValue && sentDate.Value <= DateTime.UtcNow - MinimumDelay;
        }
    }
}
