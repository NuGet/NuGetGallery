// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Entities
{
    /// <summary>
    /// Indicates package deprecation status and reason(s).
    /// </summary>
    [Flags]
    public enum PackageDeprecationStatus
    {
        /// <summary>
        /// The package is not deprecated.
        /// </summary>
        NotDeprecated = 0,

        /// <summary>
        /// The package is deprecated because of some other undefined reason.
        /// </summary>
        Other = 1,

        /// <summary>
        /// The package is deprecated because it is legacy and no longer maintained.
        /// </summary>
        Legacy = 2,

        /// <summary>
        /// The package is deprecated because it is vulnerable.
        /// </summary>
        Vulnerable = 4
    }
}