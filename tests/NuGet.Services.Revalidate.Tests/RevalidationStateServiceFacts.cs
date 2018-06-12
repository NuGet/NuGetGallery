// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Validation;
using Tests.ContextHelpers;
using Xunit;

namespace NuGet.Services.Revalidate.Tests
{
    public class RevalidationStateServiceFacts
    {
        public class TheAddPackageRevalidationsAsyncMethod : FactsBase
        {
            [Fact]
            public async Task AddsRevalidations()
            {
                // Arrange
                var revalidations = new List<PackageRevalidation>
                {
                    new PackageRevalidation { PackageId = "A" },
                    new PackageRevalidation { PackageId = "B" }
                };

                _context.Mock();
                
                // Act & Assert
                await _target.AddPackageRevalidationsAsync(revalidations);

                Assert.Equal(2, _context.Object.PackageRevalidations.Count());

                _context.Verify(c => c.SaveChangesAsync(), Times.Once);
            }
        }

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
                var result = await _target.RemoveRevalidationsAsync(5);

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
                var result = await _target.RemoveRevalidationsAsync(1);

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

        public class FactsBase
        {
            public readonly Mock<IValidationEntitiesContext> _context;
            public readonly RevalidationStateService _target;

            public FactsBase()
            {
                _context = new Mock<IValidationEntitiesContext>();

                _target = new RevalidationStateService(
                    _context.Object,
                    Mock.Of<ILogger<RevalidationStateService>>());
            }
        }
    }
}
