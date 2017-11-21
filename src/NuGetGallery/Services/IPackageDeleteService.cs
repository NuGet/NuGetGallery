// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IPackageDeleteService
    {
        Task<bool> CanPackageBeDeletedByUserAsync(Package package);
        Task SoftDeletePackagesAsync(IEnumerable<Package> packages, User deletedBy, string reason, string signature);
        Task HardDeletePackagesAsync(IEnumerable<Package> packages, User deletedBy, string reason, string signature, bool deleteEmptyPackageRegistration);
        Task ReflowHardDeletedPackageAsync(string id, string version);
    }
}