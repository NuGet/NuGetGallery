// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Status
{
    /// <summary>
    /// Describes whether or not a <see cref="IReadOnlyComponent"/> is performing as expected.
    /// </summary>
    public enum ComponentStatus
    {
        /// <summary>
        /// The <see cref="IReadOnlyComponent"/> is performing as expected.
        /// </summary>
        Up = 0,

        /// <summary>
        /// Some portion of the <see cref="IReadOnlyComponent"/> is not performing as expected.
        /// </summary>
        Degraded = 1,

        /// <summary>
        /// The <see cref="IReadOnlyComponent"/> is completely unfunctional.
        /// </summary>
        Down = 2,
    }
}
