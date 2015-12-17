// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGetGallery
{
    /// <summary>
    /// Utilities extending IPackageReaderCore
    /// </summary>
    public static class PackageReaderCoreExtensions
    {
        public static NuspecReader GetNuspecReader(this IPackageReaderCore packageReader)
        {
            return new NuspecReader(packageReader.GetNuspec());
        }
    }
}