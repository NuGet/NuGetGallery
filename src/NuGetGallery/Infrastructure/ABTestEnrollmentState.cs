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
    }
}