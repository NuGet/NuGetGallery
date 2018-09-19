// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Packaging;
using System.Linq;
using ClientPackageType = NuGet.Packaging.Core.PackageType;

namespace NuGetGallery
{
    /// <summary>
    /// Utilities extending PackageMetadata
    /// </summary>
    public static class PackageMetadataExtensions
    {
        private const string SymbolPackageTypeName = "SymbolsPackage";
        private static readonly ClientPackageType SymbolPackageType = new ClientPackageType(SymbolPackageTypeName, ClientPackageType.EmptyVersion);

        public static bool IsSymbolPackage(this PackageMetadata metadata)
        {
            var packageTypes = metadata.GetPackageTypes();
            return packageTypes.Any()
                && packageTypes.Count() == 1
                && packageTypes.First() == SymbolPackageType;
        }
    }
}