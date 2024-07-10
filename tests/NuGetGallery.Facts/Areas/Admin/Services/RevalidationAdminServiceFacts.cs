// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NuGet.Services.Validation;
using Xunit;

namespace NuGetGallery.Areas.Admin.Services
{
    public class RevalidationAdminServiceFacts
    {
        public class TheGetStatisticsAsyncMethod : FactsBase
        {
            [Theory]
            [MemberData(nameof(ReturnsExpectedResultsData))]
            public void ReturnsExpectedResults(
                PackageRevalidation[] revalidations,
                int expectedStarted,
                int expectedStartedInLastHour,
                int expectedPending)
            {
                Mock(revalidations);

                var stats = _target.GetStatistics();

                Assert.Equal(expectedStarted, stats.StartedRevalidations);
                Assert.Equal(expectedStartedInLastHour, stats.StartedRevalidationsInLastHour);
                Assert.Equal(expectedPending, stats.PendingRevalidations);
            }

            public static IEnumerable<object[]> ReturnsExpectedResultsData()
            {
                yield return new object[]
                {
                    /* revalidations: */ Array.Empty<PackageRevalidation>(),
                    /* expectedStarted: */ 0,
                    /* expectedStartedInLastHour: */ 0,
                    /* expectedPending: */ 0,
                };

                yield return new object[]
                {
                    /* revalidations: */ new[]
                    {
                        new PackageRevalidation { Enqueued = null },
                        new PackageRevalidation { Enqueued = null },
                        new PackageRevalidation { Enqueued = null },
                    },
                    /* expectedStarted: */ 0,
                    /* expectedStartedInLastHour: */ 0,
                    /* expectedPending: */ 3,
                };

                yield return new object[]
                {
                    /* revalidations: */ new[]
                    {
                        new PackageRevalidation { Enqueued = DateTime.UtcNow.Subtract(TimeSpan.FromHours(2)) },
                        new PackageRevalidation { Enqueued = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(2)) },
                        new PackageRevalidation { Enqueued = null },
                    },
                    /* expectedStarted: */ 2,
                    /* expectedStartedInLastHour: */ 1,
                    /* expectedPending: */ 1,
                };
            }
        }

        public abstract class FactsBase
        {
            protected readonly Mock<IEntityRepository<PackageRevalidation>> _revalidations;
            protected readonly RevalidationAdminService _target;

            public FactsBase()
            {
                _revalidations = new Mock<IEntityRepository<PackageRevalidation>>();

                _target = new RevalidationAdminService(_revalidations.Object);
            }

            protected void Mock(PackageRevalidation[] revalidations)
            {
                _revalidations
                    .Setup(x => x.GetAll())
                    .Returns(() => revalidations.AsQueryable());
            }
        }
    }
}
