// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public interface ICuratedFeedService
    {
        CuratedPackage CreatedCuratedPackage(
            CuratedFeed curatedFeed,
            PackageRegistration packageRegistration,
            bool included = false,
            bool automaticallyCurated = false,
            string notes = null,
            bool commitChanges = true);

        void DeleteCuratedPackage(
            int curatedFeedKey,
            int curatedPackageKey);

        void ModifyCuratedPackage(
            int curatedFeedKey,
            int curatedPackageKey,
            bool included);

        CuratedFeed GetFeedByName(string name, bool includePackages);
        CuratedFeed GetFeedByKey(int key, bool includePackages);
        IEnumerable<CuratedFeed> GetFeedsForManager(int managerKey);
        IQueryable<Package> GetPackages(string curatedFeedName);
        IQueryable<PackageRegistration> GetPackageRegistrations(string curatedFeedName);
        int? GetKey(string curatedFeedName);
    }
}
