// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface ICuratedFeedService
    {
        CuratedFeed GetFeedByName(string name);
        IQueryable<Package> GetPackages(string curatedFeedName);
    }
}
