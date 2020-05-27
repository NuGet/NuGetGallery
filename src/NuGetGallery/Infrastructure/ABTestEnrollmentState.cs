// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public enum ABTestEnrollmentState
    {
        /// <summary>
        /// The user was enrolled for A/B testing in the current request.
        /// </summary>
        FirstHit,

        /// <summary>
        /// The user is already enrolled in the latest version A/B testing.
        /// </summary>
        Active,

        /// <summary>
        /// The user was already enrolled in the previous version of A/B testing and will now be upgraded to a new version.
        /// </summary>
        Upgraded,
    }
}