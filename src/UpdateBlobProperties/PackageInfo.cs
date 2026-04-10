// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGetGallery;

namespace UpdateBlobProperties
{
    public class PackageInfo
    {
        public int Key { get; }
        public string Id { get; }
        public string Version { get; }

        public PackageInfo(int key, string id)
        {
            Key = key;
            Id = id;
        }

        public PackageInfo(int key, string id, string version)
            : this(key, id)
        {
            Version = version;
        }

        public static Func<IEntityRepository<Package>, int, int, int, Task<List<PackageInfo>>>
            GetPageOfPackageInfosWithIdOrderedByPackageRegistrationKeyAsync = async (packageRepository, pageStartKey, maxKey, maxPageSize) =>
            {
                var prs = await packageRepository.GetAll()
                    .Include(p => p.PackageRegistration)
                    .Where(p => p.PackageRegistration.Key >= pageStartKey && p.PackageRegistration.Key <= maxKey)
                    .Select(p => new { p.PackageRegistration.Key, p.PackageRegistration.Id })
                    .Distinct()
                    .OrderBy(pr => pr.Key)
                    .Take(maxPageSize)
                    .ToListAsync();

                var pis = prs.Select(pr => new PackageInfo(pr.Key, pr.Id))
                    .OrderBy(pr => pr.Key)
                    .ToList();

                return pis;
            };

        public static Func<IEntityRepository<Package>, int, int, int, Task<List<PackageInfo>>>
            GetPageOfPackageInfosWithIdAndVersionOrderedByPackageKeyAsync = async (packageRepository, pageStartKey, maxKey, maxPageSize) =>
            {
                var ps = await packageRepository.GetAll()
                    .Include(p => p.PackageRegistration)
                    .Where(p => p.Key >= pageStartKey && p.Key <= maxKey)
                    .OrderBy(p => p.Key)
                    .Take(maxPageSize)
                    .ToListAsync();

                var pis = ps.Select(p => new PackageInfo(p.Key, p.PackageRegistration.Id, p.NormalizedVersion))
                    .OrderBy(p => p.Key)
                    .ToList();

                return pis;
            };
    }
}
