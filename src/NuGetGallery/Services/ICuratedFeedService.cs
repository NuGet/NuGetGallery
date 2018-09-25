// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

namespace NuGetGallery
{
    public interface ICuratedFeedService
    {
        CuratedFeed GetFeedByName(string name, bool includePackages);
        CuratedFeed GetFeedByKey(int key, bool includePackages);
        IQueryable<Package> GetPackages(string curatedFeedName);
        IQueryable<PackageRegistration> GetPackageRegistrations(string curatedFeedName);
        int? GetKey(string curatedFeedName);
    }
}
