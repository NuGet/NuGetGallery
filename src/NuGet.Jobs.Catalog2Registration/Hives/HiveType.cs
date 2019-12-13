// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Catalog2Registration
{
    /// <summary>
    /// This represents the distinct hive versions released. See the following documentation for more details:
    /// https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource#versioning
    /// </summary>
    public enum HiveType
    {
        /// <summary>
        /// Non-gzipped blobs. Only SemVer 1.0.0 packages. This is denoted by service index type "RegistrationsBaseUrl".
        /// </summary>
        Legacy,

        /// <summary>
        /// Gzipped blobs. Only SemVer 1.0.0 packages. This is denoted by service index type "RegistrationsBaseUrl/3.4.0".
        /// </summary>
        Gzipped,

        /// <summary>
        /// Gzipped blobs. SemVer 1.0.0 and SemVer 2.0.0 packages. This is denoted by service index type "RegistrationsBaseUrl/3.6.0".
        /// </summary>
        SemVer2,
    }
}
