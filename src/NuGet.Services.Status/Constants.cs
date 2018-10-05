// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Status
{
    public static class Constants
    {
        /// <summary>
        /// Used to construct <see cref="IComponentDescription.Path"/>.
        /// </summary>
        /// <example>
        /// Suppose C is a subcomponent of B which is a subcomponent of A.
        /// The path of C is <code>"A" + ComponentPathDivider + "B" + ComponentPathDivider + "C"</code>.
        /// </example>
        public static char ComponentPathDivider = '/';
    }
}
