// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
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

        public RevalidationStatistics GetStatistics()
        {
            var recentCutoff = DateTime.UtcNow.Subtract(TimeSpan.FromHours(1));

            var pending = _revalidations.GetAll().Count(r => !r.Enqueued.HasValue && !r.Completed);
            var started = _revalidations.GetAll().Count(r => r.Enqueued.HasValue);
            var recent = _revalidations.GetAll().Count(r => r.Enqueued.HasValue && r.Enqueued >= recentCutoff);

            return new RevalidationStatistics(started, pending, recent);
        }

        public class RevalidationStatistics
        {
            public RevalidationStatistics(int started, int pending, int startedInLastHour)
            {
                StartedRevalidations = started;
                StartedRevalidationsInLastHour = startedInLastHour;
                PendingRevalidations = pending;
            }

            public int StartedRevalidations { get; }
            public int StartedRevalidationsInLastHour { get; }
            public int PendingRevalidations { get; }
        }
    }
}