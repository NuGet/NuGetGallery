// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Validation;
using Tests.ContextHelpers;
using Xunit;

namespace NuGet.Services.Revalidate.Tests.Services
{
    public class PackageRevalidationStateServiceFacts
    {
        public class TheRemoveRevalidationsAsyncMethod : FactsBase
        {
            [Fact]
            public async Task RemovesRevalidations()
            {
                // Arrange
                _context.Mock(packageRevalidations: new List<PackageRevalidation>
                {
                    new PackageRevalidation { PackageId = "A" },
                    new PackageRevalidation { PackageId = "B" }
                });

                // Act & Assert
                var result = await _target.RemovePackageRevalidationsAsync(5);

                Assert.Equal(2, result);
                Assert.Equal(0, _context.Object.PackageRevalidations.Count());

                _context.Verify(c => c.SaveChangesAsync(), Times.Once);
            }

            [Fact]
            public async Task RespectsMaxParameter()
            {
                // Arrange
                _context.Mock(packageRevalidations: new List<PackageRevalidation>
                {
                    new PackageRevalidation { PackageId = "A" },
                    new PackageRevalidation { PackageId = "B" }
                });

                // Act & Assert
                var result = await _target.RemovePackageRevalidationsAsync(1);

                Assert.Equal(1, result);
                Assert.Equal(1, _context.Object.PackageRevalidations.Count());

                _context.Verify(c => c.SaveChangesAsync(), Times.Once);
            }
        }

        public class ThePackageRevalidationCountAsyncMethod : FactsBase
        {
            [Fact]
            public async Task ReturnsRevalidationCount()
            {
                _context.Mock(packageRevalidations: new List<PackageRevalidation>
                {
                    new PackageRevalidation { PackageId = "A" },
                    new PackageRevalidation { PackageId = "B" }
                });

                Assert.Equal(2, await _target.PackageRevalidationCountAsync());
            }
        }

        public class TheCountRevalidationsEnqueuedInPastHourAsyncMethod : FactsBase
        {
            [Fact]
            public async Task ReturnsRevalidationCount()
            {
                var now = DateTime.UtcNow;

                _context.Mock(packageRevalidations: new List<PackageRevalidation>
                {
                    new PackageRevalidation { PackageId = "A", Enqueued = now.Subtract(TimeSpan.FromDays(4)) },
                    new PackageRevalidation { PackageId = "B", Enqueued = now.Subtract(TimeSpan.FromHours(3)) },
                    new PackageRevalidation { PackageId = "C", Enqueued = now.Subtract(TimeSpan.FromMinutes(2)) },
                    new PackageRevalidation { PackageId = "D", Enqueued = now.Subtract(TimeSpan.FromSeconds(1)) },
                });

                Assert.Equal(2, await _target.CountRevalidationsEnqueuedInPastHourAsync());
            }
        }

        public class FactsBase
        {
            public readonly Mock<IValidationEntitiesContext> _context;
            public readonly PackageRevalidationStateService _target;

            public FactsBase()
            {
                _context = new Mock<IValidationEntitiesContext>();

                _target = new PackageRevalidationStateService(
                    _context.Object,
                    Mock.Of<IPackageRevalidationInserter>(),
                    Mock.Of<ILogger<PackageRevalidationStateService>>());
            }
        }
    }
}
