﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class PackageIdsQuery : IPackageIdsQuery
    {
        private const string PartialIdSqlFormat = @"SELECT TOP 30 pr.ID
FROM Packages p
    JOIN PackageRegistrations pr on pr.[Key] = p.PackageRegistrationKey
WHERE pr.ID LIKE {{0}}
    {0}
GROUP BY pr.ID
ORDER BY pr.ID";

        private const string NoPartialIdSql = @"SELECT TOP 30 pr.ID
FROM Packages p
    JOIN PackageRegistrations pr on pr.[Key] = p.PackageRegistrationKey
GROUP BY pr.ID
ORDER BY MAX(pr.DownloadCount) DESC";

        private readonly IEntitiesContext _entities;

        public PackageIdsQuery(IEntitiesContext entities)
        {
            _entities = entities;
        }

        public Task<IEnumerable<string>> Execute(
            string partialId,
            bool? includePrerelease = false)
        {
            var dbContext = (DbContext)_entities;

            if (String.IsNullOrWhiteSpace(partialId))
            {
                return Task.FromResult(dbContext.Database.SqlQuery<string>(NoPartialIdSql).AsEnumerable());
            }

            var prereleaseFilter = String.Empty;
            if (!includePrerelease.HasValue || !includePrerelease.Value)
            {
                prereleaseFilter = "AND p.IsPrerelease = {1}";
            }
            return Task.FromResult(dbContext.Database.SqlQuery<string>(
                String.Format(CultureInfo.InvariantCulture, PartialIdSqlFormat, prereleaseFilter), partialId + "%", includePrerelease ?? false).AsEnumerable());
        }
    }
}