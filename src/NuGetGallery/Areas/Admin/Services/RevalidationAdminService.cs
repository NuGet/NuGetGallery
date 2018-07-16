// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Validation;

namespace NuGetGallery.Areas.Admin.Services
{
    public class RevalidationAdminService
    {
        private readonly IEntityRepository<PackageRevalidation> _revalidations;

        public RevalidationAdminService(IEntityRepository<PackageRevalidation> revalidations)
        {
            _revalidations = revalidations ?? throw new ArgumentNullException(nameof(revalidations));
        }

        public async Task<RevalidationStatistics> GetStatisticsAsync()
        {
            var pending = await _revalidations.GetAll().CountAsync(r => !r.Enqueued.HasValue);
            var started = await _revalidations.GetAll().CountAsync(r => r.Enqueued.HasValue);

            return new RevalidationStatistics(started, pending);
        }

        public class RevalidationStatistics
        {
            public RevalidationStatistics(int started, int pending)
            {
                StartedRevalidations = started;
                PendingRevalidations = pending;
            }

            public int StartedRevalidations { get; }
            public int PendingRevalidations { get; }
        }
    }
}