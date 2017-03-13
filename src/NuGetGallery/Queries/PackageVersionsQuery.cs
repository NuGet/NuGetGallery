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
    public class PackageVersionsQuery : IPackageVersionsQuery
    {
        private const string SqlFormat = @"SELECT p.[Version]
FROM Packages p
	JOIN PackageRegistrations pr on pr.[Key] = p.PackageRegistrationKey
WHERE pr.ID = {{0}}
	{0}";

        private readonly IEntitiesContext _entities;

        public PackageVersionsQuery(IEntitiesContext entities)
        {
            _entities = entities;
        }

        public Task<IEnumerable<string>> Execute(
            string id,
            bool? includePrerelease = false)
        {
            if (String.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            var dbContext = (DbContext)_entities;

            var prereleaseFilter = String.Empty;
            if (!includePrerelease.HasValue || !includePrerelease.Value)
            {
                prereleaseFilter = "AND p.IsPrerelease = 0";
            }
            return Task.FromResult(dbContext.Database.SqlQuery<string>(
                String.Format(CultureInfo.InvariantCulture, SqlFormat, prereleaseFilter), id).AsEnumerable());
        }
    }
}