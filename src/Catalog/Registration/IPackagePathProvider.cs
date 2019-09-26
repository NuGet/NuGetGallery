// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Metadata.Catalog.Registration
{
    /// <summary>
    /// An interface for providing the relative path of a package or an icon in the base address
    /// </summary>
    public interface IPackagePathProvider
    {
        string GetIconPath(string id, string version);
        string GetIconPath(string id, string version, bool normalize);
        string GetPackagePath(string id, string version);
    }
}
