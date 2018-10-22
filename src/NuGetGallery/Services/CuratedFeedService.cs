// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class CuratedFeedService : ICuratedFeedService
    {
        protected IEntityRepository<CuratedFeed> CuratedFeedRepository { get; set; }
        protected IAppConfiguration AppConfiguration { get; set; }

        protected CuratedFeedService()
        {
        }

        public CuratedFeedService(
            IEntityRepository<CuratedFeed> curatedFeedRepository,
            IAppConfiguration appConfiguration)
        {
            CuratedFeedRepository = curatedFeedRepository;
            AppConfiguration = appConfiguration;
        }

        public CuratedFeed GetFeedByName(string name)
        {
            if (IsCuratedFeedDisabled(name))
            {
                return null;
            }

            return CuratedFeedRepository
                .GetAll()
                .SingleOrDefault(cf => cf.Name == name);
        }

        public IQueryable<Package> GetPackages(string curatedFeedName)
        {
            if (IsCuratedFeedDisabled(curatedFeedName))
            {
                return Enumerable.Empty<Package>().AsQueryable();
            }

            var packages = CuratedFeedRepository.GetAll()
                .Where(cf => cf.Name == curatedFeedName)
                .SelectMany(cf => cf.Packages.SelectMany(cp => cp.PackageRegistration.Packages));

            return packages;
        }

        private bool IsCuratedFeedDisabled(string name)
        {
            if (AppConfiguration.DisabledCuratedFeeds == null)
            {
                return false;
            }

            return AppConfiguration
                .DisabledCuratedFeeds
                .Contains(name, StringComparer.OrdinalIgnoreCase);
        }
    }
}
