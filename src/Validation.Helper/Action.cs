// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Validation.Helper
{
    public enum Action
    {
        /// <summary>
        /// Request to rescan the package
        /// </summary>
        Rescan,

        /// <summary>
        /// Mark package as clean manually
        /// </summary>
        MarkClean
    }
}
