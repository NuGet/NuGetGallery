// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using System.Collections.Generic;

namespace NuGetGallery.Frameworks
{
    public interface IPackageFrameworkCompatibilityFactory
    {
        PackageFrameworkCompatibility Create(ICollection<PackageFramework> packageFrameworks);
    }
}
