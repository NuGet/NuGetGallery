// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Status
{
    /// <summary>
    /// Describes whether or not the component is performing as expected.
    /// </summary>
    public enum ComponentStatus
    {
        /// <summary>
        /// The component is performing as expected.
        /// </summary>
        Up = 0,

        /// <summary>
        /// Some portion of the component is not performing as expected.
        /// </summary>
        Degraded = 1,

        /// <summary>
        /// The component is completely unfunctional.
        /// </summary>
        Down = 2,
    }
}
