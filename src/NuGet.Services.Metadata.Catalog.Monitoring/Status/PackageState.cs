// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// The state of a package's metadata.
    /// </summary>
    public enum PackageState
    {
        /// <summary>
        /// The package is in a valid state.
        /// Its metadata appears exactly as expected.
        /// </summary>
        Valid,

        /// <summary>
        /// The package is in an invalid state.
        /// Its metadata is missing or in an unexpected state.
        /// </summary>
        Invalid
    }
}
