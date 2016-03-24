// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Metadata.Catalog.Registration
{
    /// <summary>
    /// An interface for providing the relative path of a package in the base address
    /// </summary>
    public interface IPackagePathProvider
    {
        string GetPackagePath(string id, string version);
    }
}
