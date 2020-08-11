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
        Invalid,

        /// <summary>
        /// The package's state could not be determined.
        /// Its metadata is in an unknown state.
        /// </summary>
        /// <remarks>
        /// Typically, a package is in this state if there is a newer catalog entry for a package that needs to be processed.
        /// If a package is in this state for an extended period of time, there is likely a problem with it or the monitoring pipeline.
        /// </remarks>
        Unknown,
    }
}
