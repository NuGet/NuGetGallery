﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IPackageRenameService
    {
        IReadOnlyList<PackageRename> GetPackageRenames(PackageRegistration package);
        IReadOnlyList<PackageRename> GetPackageRenamesTo(PackageRegistration package);
    }
}