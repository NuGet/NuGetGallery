// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging;

namespace NuGetGallery
{
    /// <summary>
    /// Utilities extending PackageArchiveReader
    /// </summary>
    public static class PackageArchiveReaderExtensions
    {
        public static NuspecReader GetNuspecReader(this PackageArchiveReader packageArchiveReader)
        {
            return new NuspecReader(packageArchiveReader.GetNuspec());
        }
    }
}