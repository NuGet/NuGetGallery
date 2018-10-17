// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

namespace NuGetGallery.Extensions
{
    public static class SymbolPackageExtensions
    {
        public static bool IsAvailable(this SymbolPackage symbolPackage)
        {
            return symbolPackage.StatusKey == PackageStatus.Available;
        }
    }
}