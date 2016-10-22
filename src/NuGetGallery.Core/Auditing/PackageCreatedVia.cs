// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Auditing
{
    public static class PackageCreatedVia
    {
        /// <summary>
        /// Package has been created via NuGet API (nuget.exe push)
        /// </summary>
        public const string Api = "Created via API.";

        /// <summary>
        /// Package has been created via NuGet web interface (browser)
        /// </summary>
        public const string Web = "Created via web.";
    }
}