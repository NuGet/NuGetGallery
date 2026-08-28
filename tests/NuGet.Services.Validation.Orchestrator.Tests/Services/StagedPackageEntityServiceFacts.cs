// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class StagedPackageEntityServiceFacts
    {
        [Fact]
        public void FindsLatestAttemptByIdAndVersion()
        {
            var attempts = new[]
            {
                CreateStagedPackage(41),
                CreateStagedPackage(43),
                CreateStagedPackage(42),
            };
            var target = CreateService(attempts, out _);

            var result = target.FindPackageByIdAndVersionStrict("PackageA", "1.0.0");

            Assert.Equal(43, result.Key);
        }

        [Fact]
        public void FindsExactAttemptByKey()
        {
            var expected = CreateStagedPackage(43);
            var target = CreateService(
                new[] { CreateStagedPackage(42), expected },
                out _);

            var result = target.FindPackageByKey(43);

            Assert.Same(expected, result.EntityRecord);
        }

        [Fact]
        public void ReturnsNullWhenAttemptDoesNotExist()
        {
            var target = CreateService(new[] { CreateStagedPackage(42) }, out _);

            Assert.Null(target.FindPackageByKey(43));
            Assert.Null(target.FindPackageByIdAndVersionStrict("Missing", "1.0.0"));
        }

        private static StagedPackageEntityService CreateService(
            IReadOnlyCollection<StagedPackage> stagedPackages,
            out Mock<IEntitiesContext> entitiesContext)
        {
            var query = stagedPackages.AsQueryable();
            var dbSet = new Mock<DbSet<StagedPackage>>();
            dbSet.As<IQueryable<StagedPackage>>().Setup(x => x.Provider).Returns(query.Provider);
            dbSet.As<IQueryable<StagedPackage>>().Setup(x => x.Expression).Returns(query.Expression);
            dbSet.As<IQueryable<StagedPackage>>().Setup(x => x.ElementType).Returns(query.ElementType);
            dbSet.As<IQueryable<StagedPackage>>().Setup(x => x.GetEnumerator()).Returns(() => query.GetEnumerator());
            dbSet.Setup(x => x.Include(It.IsAny<string>())).Returns(dbSet.Object);
            entitiesContext = new Mock<IEntitiesContext>();
            entitiesContext.SetupGet(x => x.StagedPackages).Returns(dbSet.Object);

            return new StagedPackageEntityService(
                Mock.Of<ICorePackageService>(),
                entitiesContext.Object);
        }

        private static StagedPackage CreateStagedPackage(int key)
        {
            return new StagedPackage
            {
                Key = key,
                Package = new Package
                {
                    NormalizedVersion = "1.0.0",
                    PackageRegistration = new PackageRegistration { Id = "PackageA" },
                },
                Status = StagedPackageStatus.Validating,
            };
        }
    }
}
